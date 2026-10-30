using System.Text;
using System.Text.Json;
using LedgerService.Data;
using LedgerService.Events;
using LedgerService.Entities;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace LedgerService.Messaging;

public sealed class TransferCompletedConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<TransferCompletedConsumer> logger) : BackgroundService
{
    private IConnection? connection;
    private IModel? channel;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:HostName"] ?? "rabbitmq",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:UserName"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        connection = factory.CreateConnection();
        channel = connection.CreateModel();
        channel.ExchangeDeclare("digitalwallet.events", ExchangeType.Topic, durable: true);
        channel.QueueDeclare(
            "ledger.transfer-completed",
            durable: true,
            exclusive: false,
            autoDelete: false);
        channel.QueueBind(
            "ledger.transfer-completed",
            "digitalwallet.events",
            "transfer.completed");
        channel.BasicQos(0, 1, false);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += (_, args) => HandleMessageAsync(args).GetAwaiter().GetResult();
        channel.BasicConsume("ledger.transfer-completed", autoAck: false, consumer);

        stoppingToken.Register(() => channel.Close());
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs args)
    {
        try
        {
            var message = JsonSerializer.Deserialize<TransferCompletedEvent>(
                Encoding.UTF8.GetString(args.Body.Span))
                ?? throw new InvalidOperationException("Invalid transfer event.");

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

            var referenceId = message.TransferId.ToString();
            var alreadyProcessed = await db.LedgerEntries.AnyAsync(entry =>
                entry.ReferenceId == referenceId &&
                entry.EntryType == "Debit");

            if (!alreadyProcessed)
            {
                db.LedgerEntries.AddRange(
                    new LedgerEntry
                    {
                        UserId = message.SenderUserId,
                        ReferenceId = referenceId,
                        EntryType = "Debit",
                        Amount = message.Amount,
                        Currency = message.Currency,
                        Description = $"Transfer {message.TransferId}"
                    },
                    new LedgerEntry
                    {
                        UserId = message.ReceiverUserId,
                        ReferenceId = referenceId,
                        EntryType = "Credit",
                        Amount = message.Amount,
                        Currency = message.Currency,
                        Description = $"Transfer {message.TransferId}"
                    });

                await db.SaveChangesAsync();
            }

            channel?.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process transfer completed event.");
            channel?.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public override void Dispose()
    {
        channel?.Dispose();
        connection?.Dispose();
        base.Dispose();
    }
}
