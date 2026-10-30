using Microsoft.EntityFrameworkCore;
using TransferService.Entities;

namespace TransferService.Data;

public sealed class TransferDbContext(DbContextOptions<TransferDbContext> options)
    : DbContext(options)
{
    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.HasKey(transfer => transfer.Id);

            entity.HasIndex(transfer => transfer.SenderUserId);
            entity.HasIndex(transfer => transfer.ReceiverUserId);
            entity.HasIndex(transfer => transfer.Status);

            entity.Property(transfer => transfer.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(transfer => transfer.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(transfer => transfer.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(transfer => transfer.FailureReason)
                .HasMaxLength(500);
        });
    }
}
