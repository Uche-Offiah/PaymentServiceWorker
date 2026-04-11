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
using System.Threading.Channels;
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
            Console.WriteLine("RabbitMQ Consumer started...");
            var connection = await _connectionFactory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            Console.WriteLine("RabbitMQ connection created");

            var queueName = nameof(OrderCreatedEvent);
            var deadLetterQueue = $"{queueName}_dlq";

            //Declare DLQ
            await channel.QueueDeclareAsync(deadLetterQueue, true, false, false);

            var args = new Dictionary<string, object?>
            {
                {"x-dead-letter-exchange", "" },
                {"x-dead-letter-routing-key", deadLetterQueue},
            };

            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: args
             );

             Console.WriteLine("Queues declared successfully");

            var consumer =  new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(message);

                //get retry count from header
                int retryCount = 0;
                if (ea.BasicProperties.Headers != null && ea.BasicProperties.Headers.TryGetValue("x-retry", out var retryObj))
                {
                    var retryBytes = retryObj as byte[];

                    if (retryBytes != null && retryBytes.Length > 0)
                    {
                        retryCount = retryBytes[0];
                    }
                }

                try
                {
                    // Idempotency checks
                    if (await repo.ExistAsync(evt.OrderId))
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        return;
                    }

                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        OrderId = evt.OrderId,
                        Amount = evt.Amount,
                        ProcessedAt = DateTime.UtcNow
                    };

                    await repo.SaveAsync(payment);

                    Console.WriteLine($"Payment processed for Order {evt.OrderId}");

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message: {ex.Message}");

                    if (retryCount >= 3)
                    {
                        // Move to DLQ
                        await channel.BasicRejectAsync(ea.DeliveryTag, false);
                    }
                    else
                    {
                        var props = new BasicProperties();
                        props.Headers = new Dictionary<string, object?>
                        {
                            { "x-retry", new byte[] { (byte)(retryCount + 1)} }
                        };

                        await channel.BasicPublishAsync(
                            exchange: "",
                            routingKey: nameof(OrderCreatedEvent),
                            mandatory: true,
                            basicProperties: props,
                            body: ea.Body
                         );

                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                }

                

            }; 

            await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer);

            await Task.Delay(Timeout.Infinite, stoppingToken);

        }
    }
}
