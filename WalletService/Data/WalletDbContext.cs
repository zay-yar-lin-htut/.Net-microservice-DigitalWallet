using Microsoft.EntityFrameworkCore;
using WalletService.Entities;

namespace WalletService.Data;

public class WalletDbContext(DbContextOptions<WalletDbContext> options)
    : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
