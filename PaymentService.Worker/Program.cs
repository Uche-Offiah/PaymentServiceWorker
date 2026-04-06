using PaymentService.Worker;
using PaymentService.Worker.Messaging;
using PaymentService.Worker.Repositories;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<RabbitMqConsumer>();

var host = builder.Build();
host.Run();
