using DigitalWallet.Contracts;
using LedgerService.Data;
using LedgerService.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Messaging;

public sealed class TransferCompletedConsumer(
    LedgerDbContext db,
    ILogger<TransferCompletedConsumer> logger)
    : IConsumer<TransferCompletedEvent>
{
    public async Task Consume(ConsumeContext<TransferCompletedEvent> context)
    {
        var message = context.Message;
        var referenceId = message.TransferId.ToString();

        var alreadyProcessed = await db.LedgerEntries.AnyAsync(entry =>
            entry.ReferenceId == referenceId &&
            entry.EntryType == "Debit",
            context.CancellationToken);

        if (alreadyProcessed)
        {
            logger.LogInformation(
                "Transfer event {TransferId} already processed.",
                message.TransferId);
            return;
        }

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

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
