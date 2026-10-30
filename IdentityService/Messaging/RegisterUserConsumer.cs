using DigitalWallet.Contracts;
using IdentityService.Data;
using IdentityService.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Messaging;

public sealed class RegisterUserConsumer(
    IdentityDbContext db,
    ILogger<RegisterUserConsumer> logger)
    : IConsumer<RegisterUserCommand>
{
    public async Task Consume(ConsumeContext<RegisterUserCommand> context)
    {
        var request = await db.RegistrationRequests
            .SingleOrDefaultAsync(
                item => item.Id == context.Message.RequestId,
                context.CancellationToken);

        if (request is null || request.Status == "Completed")
        {
            return;
        }

        request.Status = "Processing";
        await db.SaveChangesAsync(context.CancellationToken);

        var emailExists = await db.Users
            .AnyAsync(user => user.Email == request.Email, context.CancellationToken);

        if (emailExists)
        {
            request.Status = "Failed";
            request.FailureReason = "Email already exists.";
            await db.SaveChangesAsync(context.CancellationToken);
            return;
        }

        db.Users.Add(new User
        {
            Email = request.Email,
            PasswordHash = request.PasswordHash
        });

        request.Status = "Completed";
        request.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Registration request {RequestId} completed.",
            request.Id);
    }
}
