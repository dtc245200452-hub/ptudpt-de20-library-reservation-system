using System.Text;
using System.Text.Json;
using Library.Contracts.Events;
using RabbitMQ.Client;

namespace Library.Api.Services;

public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _isInitialized;

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_isInitialized && _channel is { IsOpen: true })
        {
            return;
        }

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_isInitialized && _channel is { IsOpen: true })
            {
                return;
            }

            var host = _config["RabbitMQ:HostName"] ?? "localhost";
            var port = int.TryParse(_config["RabbitMQ:Port"], out var p) ? p : 5672;
            var user = _config["RabbitMQ:UserName"] ?? "guest";
            var pass = _config["RabbitMQ:Password"] ?? "guest";
            var queueName = _config["RabbitMQ:QueueName"] ?? "book-reservations";
            var dlxExchange = "dlx.book-reservations";
            var dlqQueue = $"{queueName}.dlq";

            _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port} with user {User}...", host, port, user);

            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = user,
                Password = pass,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            // Retry connection loop (up to 5 times)
            int attempts = 0;
            while (attempts < 5)
            {
                try
                {
                    attempts++;
                    _connection = await factory.CreateConnectionAsync(ct);
                    _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to connect to RabbitMQ (attempt {Attempt}/5). Retrying in 2s...", attempts);
                    if (attempts >= 5) throw;
                    await Task.Delay(2000, ct);
                }
            }

            // 1. Declare Dead-Letter Exchange & Dead-Letter Queue (Yêu cầu mở rộng E1)
            await _channel!.ExchangeDeclareAsync(
                exchange: dlxExchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: ct
            );

            await _channel.QueueDeclareAsync(
                queue: dlqQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct
            );

            await _channel.QueueBindAsync(
                queue: dlqQueue,
                exchange: dlxExchange,
                routingKey: dlqQueue,
                cancellationToken: ct
            );

            // 2. Declare Main Queue with DLX configuration
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
                cancellationToken: ct
            );

            _isInitialized = true;
            _logger.LogInformation("RabbitMQ initialized successfully. Queue '{Queue}' and DLQ '{Dlq}' ready.", queueName, dlqQueue);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task PublishReservationCreatedAsync(ReservationCreatedEvent evt, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var queueName = _config["RabbitMQ:QueueName"] ?? "book-reservations";
        var json = JsonSerializer.Serialize(evt);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            CorrelationId = evt.CorrelationId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _channel!.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: true,
            basicProperties: props,
            body: body,
            cancellationToken: ct
        );

        _logger.LogInformation(
            "[CorrelationId: {CorrelationId}] Published ReservationCreatedEvent for Reservation {ReservationId} to queue '{Queue}'",
            evt.CorrelationId, evt.ReservationId, queueName);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync();
                _channel.Dispose();
            }

            if (_connection is not null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing RabbitMQ connection/channel.");
        }
        finally
        {
            _connectionLock.Dispose();
        }
    }
}
