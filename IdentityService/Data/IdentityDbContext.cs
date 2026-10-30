using Microsoft.EntityFrameworkCore;
using IdentityService.Entities;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

namespace IdentityService.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<RegistrationRequest> RegistrationRequests => Set<RegistrationRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);

            entity.Property(user => user.Email)
                .HasMaxLength(254)
                .IsRequired();

            entity.HasIndex(user => user.Email)
                .IsUnique();

            entity.Property(user => user.PasswordHash)
                .HasMaxLength(512)
                .IsRequired();

            entity.Property(user => user.CreatedAtUtc)
                .IsRequired();
        });

        modelBuilder.Entity<RegistrationRequest>(entity =>
        {
            entity.HasKey(request => request.Id);
            entity.HasIndex(request => request.Email);
            entity.HasIndex(request => request.Status);
            entity.Property(request => request.Email)
                .HasMaxLength(254)
                .IsRequired();
            entity.Property(request => request.PasswordHash)
                .HasMaxLength(512)
                .IsRequired();
            entity.Property(request => request.Status)
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(request => request.FailureReason)
                .HasMaxLength(500);
        });
    }
}
