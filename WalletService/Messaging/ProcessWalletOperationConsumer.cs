using DigitalWallet.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using WalletService.Data;
using WalletService.Services;

namespace WalletService.Messaging;

public sealed class ProcessWalletOperationConsumer(
    WalletDbContext db,
    IWalletService walletService,
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
            if (operation.OperationType == "Deposit")
            {
                await walletService.DepositAsync(
                    operation.UserId,
                    operation.Amount,
                    context.CancellationToken);
            }
            else
            {
                await walletService.WithdrawAsync(
                    operation.UserId,
                    operation.Amount,
                    context.CancellationToken);
            }

            operation.Status = "Completed";
            operation.CompletedAtUtc = DateTime.UtcNow;
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
