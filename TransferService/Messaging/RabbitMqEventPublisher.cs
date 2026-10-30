using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using TransferService.Events;

namespace TransferService.Messaging;

public sealed class RabbitMqEventPublisher(IConfiguration configuration)
    : IEventPublisher
{
    public Task PublishTransferCompletedAsync(
        TransferCompletedEvent message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "rabbitmq",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare("digitalwallet.events", ExchangeType.Topic, durable: true);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;

        channel.BasicPublish(
            "digitalwallet.events",
            "transfer.completed",
            properties,
            body);

        return Task.CompletedTask;
    }
}
