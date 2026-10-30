using LedgerService.Entities;
using Microsoft.EntityFrameworkCore;

namespace LedgerService.Data;

public sealed class LedgerDbContext(DbContextOptions<LedgerDbContext> options)
    : DbContext(options)
{
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);

            entity.HasIndex(entry => entry.UserId);
            entity.HasIndex(entry => entry.ReferenceId);
            entity.HasIndex(entry => new { entry.ReferenceId, entry.EntryType })
                .IsUnique();

            entity.Property(entry => entry.ReferenceId)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(entry => entry.EntryType)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(entry => entry.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            entity.Property(entry => entry.Currency)
                .HasMaxLength(3)
                .IsRequired();

            entity.Property(entry => entry.Description)
                .HasMaxLength(500);

            entity.Property(entry => entry.CreatedAtUtc)
                .IsRequired();
        });
    }
}
