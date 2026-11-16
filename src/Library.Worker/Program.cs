using Library.Data;
using Library.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Cấu hình Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=librarydb;Username=postgres;Password=postgres";

builder.Services.AddDbContext<LibraryDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Đăng ký BackgroundService RabbitMQ Consumer
builder.Services.AddHostedService<ReservationConsumer>();

var host = builder.Build();
host.Run();
