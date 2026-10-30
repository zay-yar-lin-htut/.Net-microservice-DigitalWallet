using Microsoft.EntityFrameworkCore;
using MassTransit;
using WalletService.Contracts;
using WalletService.Data;
using WalletService.Entities;

namespace WalletService.Services;

public sealed class WalletService(
    WalletDbContext db,
    IPublishEndpoint publishEndpoint) : IWalletService
{
    public async Task<Wallet> CreateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exists = await db.Wallets
            .AnyAsync(wallet => wallet.UserId == userId, cancellationToken);

        if (exists)
        {
            throw new WalletAlreadyExistsException();
        }

        var wallet = new Wallet { UserId = userId };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync(cancellationToken);

        return wallet;
    }

    public Task<Wallet?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return db.Wallets
            .AsNoTracking()
            .SingleOrDefaultAsync(wallet => wallet.UserId == userId, cancellationToken);
    }

    public async Task<Wallet> DepositAsync(
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var wallet = await GetTrackedWalletAsync(userId, cancellationToken);
        wallet.Balance += amount;
        wallet.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return wallet;
    }

    public async Task<Wallet> WithdrawAsync(
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var wallet = await GetTrackedWalletAsync(userId, cancellationToken);

        if (wallet.Balance < amount)
        {
            throw new InsufficientBalanceException();
        }

        wallet.Balance -= amount;
        wallet.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return wallet;
    }

    public Task<Wallet> CreditInternalAsync(
        Guid userId,
        decimal amount,
        string? referenceId,
        CancellationToken cancellationToken)
    {
        return ProcessInternalAsync(userId, "Credit", amount, referenceId, cancellationToken);
    }

    public Task<Wallet> DebitInternalAsync(
        Guid userId,
        decimal amount,
        string? referenceId,
        CancellationToken cancellationToken)
    {
        return ProcessInternalAsync(userId, "Debit", amount, referenceId, cancellationToken);
    }

    public async Task<WalletOperationResponse> CreateOperationAsync(
        Guid userId,
        string operationType,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var operation = new WalletOperation
        {
            UserId = userId,
            OperationType = operationType,
            Amount = amount
        };

        db.WalletOperations.Add(operation);
        await publishEndpoint.Publish(
            new DigitalWallet.Contracts.ProcessWalletOperationCommand(operation.Id),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToOperationResponse(operation);
    }

    public Task<WalletOperationResponse?> GetOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        return db.WalletOperations
            .AsNoTracking()
            .Where(operation => operation.Id == operationId)
            .Select(operation => new WalletOperationResponse(
                operation.Id,
                operation.UserId,
                operation.OperationType,
                operation.Amount,
                operation.Status,
                operation.FailureReason,
                operation.CreatedAtUtc,
                operation.CompletedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Wallet> GetTrackedWalletAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.Wallets
            .SingleOrDefaultAsync(wallet => wallet.UserId == userId, cancellationToken)
            ?? throw new WalletNotFoundException();
    }

    private async Task<Wallet> ProcessInternalAsync(Guid userId, string type, decimal amount, string? referenceId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(referenceId))
        {
            var previous = await db.WalletOperations.AsNoTracking()
                .Where(x => x.ReferenceId == referenceId && x.Status == "Completed")
                .Select(x => x.UserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (previous != Guid.Empty)
                return await GetByUserIdAsync(userId, cancellationToken) ?? throw new WalletNotFoundException();
        }

        var operation = new WalletOperation { UserId = userId, OperationType = type, Amount = amount, ReferenceId = referenceId, Status = "Processing" };
        db.WalletOperations.Add(operation);
        var wallet = type == "Debit"
            ? await WithdrawAsync(userId, amount, cancellationToken)
            : await DepositAsync(userId, amount, cancellationToken);
        operation.Status = "Completed";
        operation.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private static WalletOperationResponse ToOperationResponse(WalletOperation operation) => new(
        operation.Id,
        operation.UserId,
        operation.OperationType,
        operation.Amount,
        operation.Status,
        operation.FailureReason,
        operation.CreatedAtUtc,
        operation.CompletedAtUtc);
}
