using PaymentService.Worker.Entities;
using PaymentService.Worker.Models;
using PaymentService.Worker.Repositories;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PaymentService.Worker.Messaging
{
    public class RabbitMqConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ConnectionFactory _connectionFactory;

        public RabbitMqConsumer(IServiceProvider serviceProvider)
        {
            _connectionFactory = new ConnectionFactory()
            {
                HostName = "localhost",
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            var queueName = nameof(OrderCreatedEvent);

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false
             );

            var consumer =  new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(message);

                if (await repo.ExistAsync(evt.OrderId))
                {
                    return;
                }

                var payment = new Payment
                {
                    Id = new Guid(),
                    OrderId = evt.OrderId,
                    Amount = evt.Amount,
                    ProcessedAt = DateTime.UtcNow
                };

                await repo.SaveAsync(payment);

                Console.WriteLine($"Payment processed for Order {evt.OrderId}");
            }; 

            await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);

            return;

        }
    }
}
