using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaymentService.Worker;
using PaymentService.Worker.Data;
using PaymentService.Worker.Messaging;
using PaymentService.Worker.Repositories;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Services.AddSerilog((services, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .WriteTo.Console()
    .Enrich.FromLogContext()
);

builder.Services.AddHostedService<Worker>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("PaymentDb"));
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddHostedService<RabbitMqConsumer>();

var host = builder.Build();

host.Run();
