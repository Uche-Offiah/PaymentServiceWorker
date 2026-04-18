using PaymentService.Worker;
using PaymentService.Worker.Messaging;
using PaymentService.Worker.Repositories;
using Microsoft.Extensions.Configuration;
using PaymentService.Worker.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Services.AddSerilog();

builder.Services.AddHostedService<Worker>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("PaymentDb"));
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddHostedService<RabbitMqConsumer>();

var host = builder.Build();

host.Run();
