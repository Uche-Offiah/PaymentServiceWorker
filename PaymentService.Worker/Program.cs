using PaymentService.Worker;
using PaymentService.Worker.Messaging;
using PaymentService.Worker.Repositories;
using Microsoft.Extensions.Configuration;
using PaymentService.Worker.Data;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("PaymentDb"));
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddHostedService<RabbitMqConsumer>();

var host = builder.Build();

host.Run();
