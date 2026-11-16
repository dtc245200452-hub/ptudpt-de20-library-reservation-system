using System.Text;
using System.Text.Json;
using Library.Contracts.Enums;
using Library.Contracts.Events;
using Library.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Library.Worker;

public class ReservationConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ReservationConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public ReservationConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ReservationConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reservation Worker BackgroundService is starting...");

        var host = _config["RabbitMQ:HostName"] ?? "localhost";
        var port = int.TryParse(_config["RabbitMQ:Port"], out var p) ? p : 5672;
        var user = _config["RabbitMQ:UserName"] ?? "guest";
        var pass = _config["RabbitMQ:Password"] ?? "guest";
        var queueName = _config["RabbitMQ:QueueName"] ?? "book-reservations";
        var dlxExchange = "dlx.book-reservations";
        var dlqQueue = $"{queueName}.dlq";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = user,
            Password = pass,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        // 1. Kết nối với RabbitMQ (có cơ chế Retry khi khởi động cùng Docker)
        int attempts = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                attempts++;
                _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port} (attempt {Attempt})...", host, port, attempts);
                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                _logger.LogInformation("Successfully connected to RabbitMQ.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ not ready yet ({Message}). Retrying in 3s...", ex.Message);
                await Task.Delay(3000, stoppingToken);
            }
        }

        if (stoppingToken.IsCancellationRequested || _channel is null)
        {
            return;
        }

        // 2. Thiết lập Dead-Letter Exchange & Queue
        await _channel.ExchangeDeclareAsync(
            exchange: dlxExchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken
        );

        await _channel.QueueDeclareAsync(
            queue: dlqQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken
        );

        await _channel.QueueBindAsync(
            queue: dlqQueue,
            exchange: dlxExchange,
            routingKey: dlqQueue,
            cancellationToken: stoppingToken
        );

        // 3. Khai báo Main Queue với Dead-Letter Queue
        var queueArgs = new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", dlxExchange },
            { "x-dead-letter-routing-key", dlqQueue }
        };

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: queueArgs,
            cancellationToken: stoppingToken
        );

        // Prefetch count = 1: Đảm bảo Worker xử lý tuần tự từng thông điệp, kiểm soát lưu lượng
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        _logger.LogInformation("Listening on queue: '{Queue}' (DLQ: '{Dlq}')...", queueName, dlqQueue);

        // 4. Đăng ký Consumer xử lý thông điệp
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var correlationId = ea.BasicProperties?.CorrelationId ?? "N/A";

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["DeliveryTag"] = ea.DeliveryTag
            }))
            {
                _logger.LogInformation("[CorrelationId: {CorrelationId}] Received message: {Json}", correlationId, json);

                try
                {
                    var evt = JsonSerializer.Deserialize<ReservationCreatedEvent>(json);
                    if (evt is null)
                    {
                        _logger.LogWarning("Invalid message format. Moving to DLQ.");
                        // Nack không requeue để chuyển thẳng vào Dead Letter Queue
                        await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                        return;
                    }

                    // Xử lý nghiệp vụ đặt mượn sách
                    await ProcessReservationAsync(evt, correlationId, stoppingToken);

                    // Xử lý thành công -> Gửi ACK
                    await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                    _logger.LogInformation("[CorrelationId: {CorrelationId}] Message acknowledged successfully.", correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[CorrelationId: {CorrelationId}] Error processing reservation message.", correlationId);

                    // Nếu có lỗi hệ thống ngoài ý muốn, nack không requeue để chuyển sang DLQ tránh loop vô tận
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                }
            }
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false, // Manual ACK cho độ tin cậy tuyệt đối
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        // Giữ BackgroundService hoạt động liên tục
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessReservationAsync(ReservationCreatedEvent evt, string correlationId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

        // 1. Kiểm tra Idempotency (Yêu cầu mở rộng E2)
        var reservation = await db.Reservations.Include(r => r.Book).FirstOrDefaultAsync(r => r.Id == evt.ReservationId, ct);
        if (reservation is null)
        {
            _logger.LogWarning("[CorrelationId: {CorrelationId}] Reservation {ReservationId} not found in DB.", correlationId, evt.ReservationId);
            return;
        }

        if (reservation.Status != ReservationStatus.Pending)
        {
            _logger.LogInformation(
                "[CorrelationId: {CorrelationId}] Reservation {ReservationId} already processed (Status: {Status}). Idempotency applied, skipping.",
                correlationId, evt.ReservationId, reservation.Status);
            return;
        }

        // 2. Chống Race Condition / Overbooking bằng Atomic SQL Update
        // Trừ AvailableCopies chỉ khi AvailableCopies > 0
        var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE ""Books"" 
               SET ""AvailableCopies"" = ""AvailableCopies"" - 1 
               WHERE ""Id"" = {evt.BookId} AND ""AvailableCopies"" > 0",
            ct
        );

        if (rowsAffected > 0)
        {
            // Đặt sách thành công -> Trạng thái Reserved
            reservation.Status = ReservationStatus.Reserved;
            reservation.ProcessedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "[CorrelationId: {CorrelationId}] SUCCESS: Book {BookId} reserved for Reservation {ReservationId}. Status -> RESERVED",
                correlationId, evt.BookId, evt.ReservationId);
        }
        else
        {
            // Hết sách khả dụng -> Trạng thái Rejected
            reservation.Status = ReservationStatus.Rejected;
            reservation.RejectReason = "Sách đã hết bản khả dụng tại thời điểm xử lý.";
            reservation.ProcessedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "[CorrelationId: {CorrelationId}] REJECTED: Book {BookId} has no available copies. Status -> REJECTED",
                correlationId, evt.BookId, evt.ReservationId);
        }

        await db.SaveChangesAsync(ct);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Reservation Worker...");
        try
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync(cancellationToken);
                _channel.Dispose();
            }

            if (_connection is not null)
            {
                await _connection.CloseAsync(cancellationToken);
                _connection.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while closing RabbitMQ channel/connection on worker stop.");
        }

        await base.StopAsync(cancellationToken);
    }
}
