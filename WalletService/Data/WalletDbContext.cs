using Microsoft.EntityFrameworkCore;
using WalletService.Entities;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

namespace WalletService.Data;

public class WalletDbContext(DbContextOptions<WalletDbContext> options)
    : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();

    public DbSet<WalletOperation> WalletOperations => Set<WalletOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(wallet => wallet.Id);

            entity.HasIndex(wallet => wallet.UserId)
                .IsUnique();

            entity.Property(wallet => wallet.Balance)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(wallet => wallet.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(wallet => wallet.CreatedAtUtc)
                .IsRequired();

            entity.Property(wallet => wallet.UpdatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<WalletOperation>(entity =>
        {
            entity.HasKey(operation => operation.Id);
            entity.HasIndex(operation => operation.UserId);
            entity.HasIndex(operation => operation.Status);
            entity.HasIndex(operation => operation.ReferenceId).IsUnique();
            entity.Property(operation => operation.ReferenceId).HasMaxLength(200);
            entity.Property(operation => operation.OperationType)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(operation => operation.Amount)
                .HasPrecision(18, 2)
                .IsRequired();
            entity.Property(operation => operation.Status)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(operation => operation.FailureReason)
                .HasMaxLength(500);
        });
    }
}
