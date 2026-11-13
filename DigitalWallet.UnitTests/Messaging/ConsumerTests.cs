using DigitalWallet.Contracts;
using IdentityService.Data;
using IdentityService.Entities;
using IdentityService.Messaging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using WalletService.Contracts;
using WalletService.Data;
using WalletService.Entities;
using WalletService.Messaging;
using WalletService.Services;
using Xunit;

namespace DigitalWallet.UnitTests.Messaging;

public class ConsumerTests
{
    private static IdentityDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private static WalletDbContext CreateWalletDbContext()
    {
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new WalletDbContext(options);
    }

    [Fact]
    public async Task RegisterUserConsumer_WhenRequestDoesNotExist_ReturnsGracefully()
    {
        // Arrange
        using var db = CreateIdentityDbContext();
        var loggerMock = new Mock<ILogger<RegisterUserConsumer>>();
        var consumer = new RegisterUserConsumer(db, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<RegisterUserCommand>>();
        contextMock.Setup(c => c.Message).Returns(new RegisterUserCommand(Guid.NewGuid()));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task RegisterUserConsumer_WhenRequestAlreadyCompleted_DoesNothing()
    {
        // Arrange
        using var db = CreateIdentityDbContext();
        var requestId = Guid.NewGuid();
        db.RegistrationRequests.Add(new RegistrationRequest
        {
            Id = requestId,
            Email = "completed@example.com",
            PasswordHash = "hashed",
            Status = "Completed"
        });
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<RegisterUserConsumer>>();
        var consumer = new RegisterUserConsumer(db, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<RegisterUserCommand>>();
        contextMock.Setup(c => c.Message).Returns(new RegisterUserCommand(requestId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task RegisterUserConsumer_WhenEmailAlreadyExists_MarksRequestAsFailed()
    {
        // Arrange
        using var db = CreateIdentityDbContext();
        var email = "duplicate@example.com";
        db.Users.Add(new User { Email = email, PasswordHash = "existing_hash" });

        var requestId = Guid.NewGuid();
        var request = new RegistrationRequest
        {
            Id = requestId,
            Email = email,
            PasswordHash = "new_hash",
            Status = "Pending"
        };
        db.RegistrationRequests.Add(request);
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<RegisterUserConsumer>>();
        var consumer = new RegisterUserConsumer(db, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<RegisterUserCommand>>();
        contextMock.Setup(c => c.Message).Returns(new RegisterUserCommand(requestId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var updated = await db.RegistrationRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal("Failed", updated.Status);
        Assert.Equal("Email already exists.", updated.FailureReason);
    }

    [Fact]
    public async Task RegisterUserConsumer_WhenValid_CreatesUserAndMarksRequestCompleted()
    {
        // Arrange
        using var db = CreateIdentityDbContext();
        var requestId = Guid.NewGuid();
        var request = new RegistrationRequest
        {
            Id = requestId,
            Email = "newuser@example.com",
            PasswordHash = "secure_hash",
            Status = "Pending"
        };
        db.RegistrationRequests.Add(request);
        await db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<RegisterUserConsumer>>();
        var consumer = new RegisterUserConsumer(db, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<RegisterUserCommand>>();
        contextMock.Setup(c => c.Message).Returns(new RegisterUserCommand(requestId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var updated = await db.RegistrationRequests.SingleAsync(r => r.Id == requestId);
        Assert.Equal("Completed", updated.Status);
        Assert.NotNull(updated.CompletedAtUtc);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == "newuser@example.com");
        Assert.NotNull(user);
        Assert.Equal("secure_hash", user.PasswordHash);
    }

    [Fact]
    public async Task ProcessWalletOperationConsumer_WhenDepositOperation_CompletesAndPublishesEvent()
    {
        // Arrange
        using var db = CreateWalletDbContext();
        var userId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var op = new WalletOperation
        {
            Id = operationId,
            UserId = userId,
            OperationType = "Deposit",
            Amount = 1000m,
            Status = "Pending"
        };
        db.WalletOperations.Add(op);
        await db.SaveChangesAsync();

        var walletServiceMock = new Mock<IWalletService>();
        walletServiceMock.Setup(w => w.DepositAsync(userId, 1000m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Wallet { UserId = userId, Balance = 1000m });

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<ProcessWalletOperationConsumer>>();

        var consumer = new ProcessWalletOperationConsumer(db, walletServiceMock.Object, publishMock.Object, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<ProcessWalletOperationCommand>>();
        contextMock.Setup(c => c.Message).Returns(new ProcessWalletOperationCommand(operationId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var updated = await db.WalletOperations.SingleAsync(o => o.Id == operationId);
        Assert.Equal("Completed", updated.Status);
        Assert.NotNull(updated.CompletedAtUtc);

        publishMock.Verify(p => p.Publish(
            It.Is<WalletOperationCompletedEvent>(e => e.OperationId == operationId && e.Amount == 1000m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessWalletOperationConsumer_WhenWithdrawFailsWithInsufficientBalance_MarksFailed()
    {
        // Arrange
        using var db = CreateWalletDbContext();
        var userId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var op = new WalletOperation
        {
            Id = operationId,
            UserId = userId,
            OperationType = "Withdraw",
            Amount = 5000m,
            Status = "Pending"
        };
        db.WalletOperations.Add(op);
        await db.SaveChangesAsync();

        var walletServiceMock = new Mock<IWalletService>();
        walletServiceMock.Setup(w => w.WithdrawAsync(userId, 5000m, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientBalanceException());

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<ProcessWalletOperationConsumer>>();

        var consumer = new ProcessWalletOperationConsumer(db, walletServiceMock.Object, publishMock.Object, loggerMock.Object);

        var contextMock = new Mock<ConsumeContext<ProcessWalletOperationCommand>>();
        contextMock.Setup(c => c.Message).Returns(new ProcessWalletOperationCommand(operationId));
        contextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);

        // Act
        await consumer.Consume(contextMock.Object);

        // Assert
        var updated = await db.WalletOperations.SingleAsync(o => o.Id == operationId);
        Assert.Equal("Failed", updated.Status);
        Assert.NotNull(updated.FailureReason);
        Assert.Contains("Insufficient balance", updated.FailureReason);
    }
}
