using Microsoft.EntityFrameworkCore;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using TransferService.Entities;

namespace TransferService.Data;

public sealed class TransferDbContext(DbContextOptions<TransferDbContext> options)
    : DbContext(options)
{
    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<TransferStep> TransferSteps => Set<TransferStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.HasKey(transfer => transfer.Id);

            entity.HasIndex(transfer => transfer.IdempotencyKey).IsUnique();
            entity.Property(transfer => transfer.IdempotencyKey)
                .HasMaxLength(200)
                .IsRequired();

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

        modelBuilder.Entity<TransferStep>(entity =>
        {
            entity.HasKey(step => step.Id);
            entity.HasIndex(step => new { step.TransferId, step.StepType }).IsUnique();
            entity.Property(step => step.StepType).HasMaxLength(40).IsRequired();
            entity.Property(step => step.Status).HasMaxLength(20).IsRequired();
            entity.Property(step => step.FailureReason).HasMaxLength(500);
        });
    }
}
