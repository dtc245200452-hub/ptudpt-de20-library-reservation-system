using Library.Api.Endpoints;
using Library.Api.Middleware;
using Library.Api.Services;
using Library.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Library Reservation System API",
        Version = "v1",
        Description = "API cho Hệ thống Thư viện và Đặt mượn sách phân tán (Đề tài 20 - PTUDPT)"
    });
});

// 2. Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=librarydb;Username=postgres;Password=postgres";

builder.Services.AddDbContext<LibraryDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// 3. RabbitMQ Publisher (Singleton)
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

// 4. Health Checks: kiểm tra cả PostgreSQL lẫn RabbitMQ (Yêu cầu mở rộng E4)
var rabbitHost = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitPort = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var rp) ? rp : 5672;
var rabbitUser = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitConnectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:{rabbitPort}/";

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "PostgreSQL", tags: new[] { "db", "ready" })
    .AddRabbitMQ(rabbitConnectionString: rabbitConnectionString, name: "RabbitMQ", tags: new[] { "mq", "ready" });

// 5. CORS configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Library API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors();

// Correlation ID Middleware (Distributed Tracing)
app.UseMiddleware<CorrelationIdMiddleware>();

// Health check endpoint với JSON chi tiết từng thành phần
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds + " ms",
            components = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds + " ms"
            })
        }, new JsonSerializerOptions { WriteIndented = true });

        await context.Response.WriteAsync(result);
    }
});

// Minimal API Route Groups
app.MapGroup("/books")
   .WithTags("Books")
   .MapBookEndpoints();

app.MapGroup("/reservations")
   .WithTags("Reservations")
   .MapReservationEndpoints();

app.MapGroup("/users")
   .WithTags("Users")
   .MapUserEndpoints();

// Root endpoint for quick sanity check
app.MapGet("/", () => Results.Ok(new
{
    system = "Library Reservation System API",
    status = "Running",
    timestamp = DateTime.UtcNow,
    documentation = "/swagger",
    healthCheck = "/healthz"
}));

// Automatic database migration/creation on startup with retry
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

    logger.LogInformation("Verifying database connectivity and ensuring schema...");
    int maxRetries = 10;
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            await db.Database.EnsureCreatedAsync();
            logger.LogInformation("Database verified and schema ensured successfully.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to connect to database (attempt {Attempt}/{MaxRetries}). Retrying in 3s...", attempt, maxRetries);
            if (attempt == maxRetries)
            {
                logger.LogError(ex, "Could not establish database connection after multiple attempts.");
                throw;
            }
            await Task.Delay(3000);
        }
    }
}

app.Run();
