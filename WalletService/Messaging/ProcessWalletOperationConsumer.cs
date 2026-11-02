using DigitalWallet.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using WalletService.Data;
using WalletService.Entities;
using WalletService.Services;

namespace WalletService.Messaging;

public sealed class ProcessWalletOperationConsumer(
    WalletDbContext db,
    IWalletService walletService,
    IPublishEndpoint publishEndpoint,
    ILogger<ProcessWalletOperationConsumer> logger)
    : IConsumer<ProcessWalletOperationCommand>
{
    public async Task Consume(ConsumeContext<ProcessWalletOperationCommand> context)
    {
        var operation = await db.WalletOperations
            .SingleOrDefaultAsync(
                item => item.Id == context.Message.OperationId,
                context.CancellationToken);

        if (operation is null || operation.Status == "Completed")
        {
            return;
        }

        operation.Status = "Processing";
        await db.SaveChangesAsync(context.CancellationToken);

        try
        {
            Wallet wallet;
            if (operation.OperationType == "Deposit")
            {
                wallet = await walletService.DepositAsync(
                    operation.UserId,
                    operation.Amount,
                    context.CancellationToken);
            }
            else
            {
                wallet = await walletService.WithdrawAsync(
                    operation.UserId,
                    operation.Amount,
                    context.CancellationToken);
            }

            operation.Status = "Completed";
            operation.CompletedAtUtc = DateTime.UtcNow;
            await publishEndpoint.Publish(new WalletOperationCompletedEvent(
                operation.Id,
                operation.UserId,
                operation.OperationType,
                operation.Amount,
                wallet.Currency,
                operation.CompletedAtUtc.Value), context.CancellationToken);
        }
        catch (Exception exception)
        {
            operation.Status = "Failed";
            operation.FailureReason = exception.Message[..Math.Min(exception.Message.Length, 500)];
            logger.LogWarning(exception, "Wallet operation {OperationId} failed.", operation.Id);
        }

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
