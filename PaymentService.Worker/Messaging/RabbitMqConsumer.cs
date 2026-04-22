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

        private readonly ILogger<RabbitMqConsumer> _logger;

        public RabbitMqConsumer(IServiceProvider serviceProvider, ILogger<RabbitMqConsumer> logger)
        {
            _connectionFactory = new ConnectionFactory()
            {
                HostName = "localhost",
            };
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("RabbitMQ Consumer started...");

            while (!stoppingToken.IsCancellationRequested)
            {
                IConnection? connection = null;
                IChannel? channel = null;
                try
                {
                    (connection, channel) = await StartConsumer(stoppingToken);

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        if (connection == null || channel == null ||
                            connection.IsOpen == false || channel.IsClosed)
                        {
                            throw new Exception("RabbitMQ connection/channel closed");
                        }

                        await Task.Delay(2000, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Connection lost: {ex.Message}");
                    _logger.LogError(ex, "Connection lost ");
                }
                finally
                {
                    if (channel != null && channel.IsOpen)
                        await channel.CloseAsync();

                    if (connection != null && connection.IsOpen)
                        await connection.CloseAsync();
                }
                _logger.LogInformation("Reconnecting in 5 seconds...");
                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task<(IConnection connection, IChannel channel)> StartConsumer(CancellationToken stoppingToken)
        {
            var connection = await _connectionFactory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            Console.WriteLine("RabbitMQ connection created");

            var queueName = nameof(OrderCreatedEvent);
            var deadLetterQueue = $"{queueName}_dlq";

            // Declare DLQ
            await channel.QueueDeclareAsync(deadLetterQueue, true, false, false);

            var args = new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", deadLetterQueue },
            };

            await channel.QueueDeclareAsync(queueName, true, false, false, args);

            Console.WriteLine("Queues declared successfully");

            // Quality of service
            await channel.BasicQosAsync(0, 1, false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                using var scope = _serviceProvider.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(message);

                if (evt == null)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    return;
                }

                int retryCount = 0;

                var headers = ea.BasicProperties?.Headers;
                if (headers != null &&
                    headers.TryGetValue("x-retry", out var retryObj) &&
                    retryObj is byte[] bytes &&
                    bytes.Length > 0)
                {
                    retryCount = bytes[0];
                }

                try
                {
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
                    _logger.LogInformation("Payment processed for Order {OrderId}", evt.OrderId);
                    

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    _logger.LogError(ex, "Error processing message");

                    if (retryCount >= 3)
                    {
                        await channel.BasicRejectAsync(ea.DeliveryTag, false);
                    }
                    else
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                        await Task.Delay(delay, stoppingToken);

                        var props = new BasicProperties
                        {
                            Headers = new Dictionary<string, object?>
                            {
                                ["x-retry"] = new byte[] { (byte)(retryCount + 1) }
                            }
                        };

                        await channel.BasicPublishAsync(
                            "",
                            queueName,
                            false,
                            props,
                            ea.Body
                        );

                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                }
            };

            await channel.BasicConsumeAsync(queueName, false, consumer);

            _logger.LogInformation("Consumer started....");
            return (connection, channel);
        }

    }
}
