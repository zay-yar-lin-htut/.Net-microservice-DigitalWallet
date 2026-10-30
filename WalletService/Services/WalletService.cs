using Microsoft.EntityFrameworkCore;
using WalletService.Data;
using WalletService.Entities;

namespace WalletService.Services;

public sealed class WalletService(WalletDbContext db) : IWalletService
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
        CancellationToken cancellationToken)
    {
        return DepositAsync(userId, amount, cancellationToken);
    }

    public Task<Wallet> DebitInternalAsync(
        Guid userId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        return WithdrawAsync(userId, amount, cancellationToken);
    }

    private async Task<Wallet> GetTrackedWalletAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await db.Wallets
            .SingleOrDefaultAsync(wallet => wallet.UserId == userId, cancellationToken)
            ?? throw new WalletNotFoundException();
    }
}
