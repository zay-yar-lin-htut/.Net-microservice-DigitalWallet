using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using WalletService.Data;
using WalletService.Entities;
using WalletService.Services;
using Xunit;

namespace DigitalWallet.UnitTests.Services;

public class WalletServiceTests
{
    private static WalletDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WalletDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new WalletDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_WhenWalletDoesNotExist_CreatesNewWallet()
    {
        // Arrange
        using var db = CreateDbContext();
        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);
        var userId = Guid.NewGuid();

        // Act
        var result = await service.CreateAsync(userId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(0m, result.Balance);
        Assert.Equal("MMK", result.Currency);

        var saved = await db.Wallets.SingleOrDefaultAsync(w => w.UserId == userId);
        Assert.NotNull(saved);
        Assert.Equal(result.Id, saved.Id);
    }

    [Fact]
    public async Task CreateAsync_WhenWalletAlreadyExists_ThrowsWalletAlreadyExistsException()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        db.Wallets.Add(new Wallet { UserId = userId, Balance = 500m });
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<WalletAlreadyExistsException>(() =>
            service.CreateAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task GetByUserIdAsync_WhenWalletExists_ReturnsWallet()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { UserId = userId, Balance = 1000m };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act
        var result = await service.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(1000m, result.Balance);
    }

    [Fact]
    public async Task GetByUserIdAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act
        var result = await service.GetByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DepositAsync_WhenWalletExists_IncreasesBalanceAndUpdatesTimestamp()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var initialTime = DateTime.UtcNow.AddHours(-1);
        var wallet = new Wallet { UserId = userId, Balance = 500m, UpdatedAtUtc = initialTime };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act
        var result = await service.DepositAsync(userId, 250m, CancellationToken.None);

        // Assert
        Assert.Equal(750m, result.Balance);
        Assert.True(result.UpdatedAtUtc > initialTime);

        var updated = await db.Wallets.SingleAsync(w => w.UserId == userId);
        Assert.Equal(750m, updated.Balance);
    }

    [Fact]
    public async Task DepositAsync_WhenWalletNotFound_ThrowsWalletNotFoundException()
    {
        // Arrange
        using var db = CreateDbContext();
        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<WalletNotFoundException>(() =>
            service.DepositAsync(Guid.NewGuid(), 100m, CancellationToken.None));
    }

    [Fact]
    public async Task WithdrawAsync_WhenSufficientFunds_DecreasesBalance()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { UserId = userId, Balance = 1000m };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act
        var result = await service.WithdrawAsync(userId, 400m, CancellationToken.None);

        // Assert
        Assert.Equal(600m, result.Balance);

        var updated = await db.Wallets.SingleAsync(w => w.UserId == userId);
        Assert.Equal(600m, updated.Balance);
    }

    [Fact]
    public async Task WithdrawAsync_WhenInsufficientFunds_ThrowsInsufficientBalanceException()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { UserId = userId, Balance = 200m };
        db.Wallets.Add(wallet);
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InsufficientBalanceException>(() =>
            service.WithdrawAsync(userId, 500m, CancellationToken.None));
    }

    [Fact]
    public async Task WithdrawAsync_WhenWalletNotFound_ThrowsWalletNotFoundException()
    {
        // Arrange
        using var db = CreateDbContext();
        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<WalletNotFoundException>(() =>
            service.WithdrawAsync(Guid.NewGuid(), 50m, CancellationToken.None));
    }

    [Fact]
    public async Task CreateOperationAsync_WithValidInput_AddsOperationAndPublishesCommand()
    {
        // Arrange
        using var db = CreateDbContext();
        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);
        var userId = Guid.NewGuid();

        // Act
        var response = await service.CreateOperationAsync(userId, "Deposit", 1500m, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(userId, response.UserId);
        Assert.Equal("Deposit", response.OperationType);
        Assert.Equal(1500m, response.Amount);
        Assert.Equal("Pending", response.Status);

        var saved = await db.WalletOperations.SingleOrDefaultAsync(o => o.Id == response.OperationId);
        Assert.NotNull(saved);
        Assert.Equal(1500m, saved.Amount);

        publishMock.Verify(p => p.Publish(
            It.Is<DigitalWallet.Contracts.ProcessWalletOperationCommand>(cmd => cmd.OperationId == response.OperationId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOperationAsync_WhenExists_ReturnsOperationResponse()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var operation = new WalletOperation
        {
            UserId = userId,
            OperationType = "Withdraw",
            Amount = 300m,
            Status = "Completed"
        };
        db.WalletOperations.Add(operation);
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act
        var result = await service.GetOperationAsync(operation.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(operation.Id, result.OperationId);
        Assert.Equal("Withdraw", result.OperationType);
        Assert.Equal(300m, result.Amount);
        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task GetOperationAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act
        var result = await service.GetOperationAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreditInternalAsync_WhenNewReference_CreditsWalletAndCompletesOperation()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        db.Wallets.Add(new Wallet { UserId = userId, Balance = 100m });
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);
        var refId = "ref-credit-100";

        // Act
        var result = await service.CreditInternalAsync(userId, 50m, refId, CancellationToken.None);

        // Assert
        Assert.Equal(150m, result.Balance);
        var op = await db.WalletOperations.SingleOrDefaultAsync(o => o.ReferenceId == refId);
        Assert.NotNull(op);
        Assert.Equal("Completed", op.Status);
        Assert.Equal("Credit", op.OperationType);
    }

    [Fact]
    public async Task DebitInternalAsync_WhenNewReference_DebitsWalletAndCompletesOperation()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        db.Wallets.Add(new Wallet { UserId = userId, Balance = 500m });
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);
        var refId = "ref-debit-100";

        // Act
        var result = await service.DebitInternalAsync(userId, 200m, refId, CancellationToken.None);

        // Assert
        Assert.Equal(300m, result.Balance);
        var op = await db.WalletOperations.SingleOrDefaultAsync(o => o.ReferenceId == refId);
        Assert.NotNull(op);
        Assert.Equal("Completed", op.Status);
        Assert.Equal("Debit", op.OperationType);
    }

    [Fact]
    public async Task CreditInternalAsync_WhenReferenceAlreadyCompleted_ReturnsExistingWalletWithoutDuplicateCredit()
    {
        // Arrange
        using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        db.Wallets.Add(new Wallet { UserId = userId, Balance = 500m });
        var refId = "ref-duplicate-check";
        db.WalletOperations.Add(new WalletOperation
        {
            UserId = userId,
            ReferenceId = refId,
            OperationType = "Credit",
            Amount = 100m,
            Status = "Completed"
        });
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var service = new WalletService.Services.WalletService(db, publishMock.Object);

        // Act
        var result = await service.CreditInternalAsync(userId, 100m, refId, CancellationToken.None);

        // Assert
        // Balance remains 500m, not 600m (idempotent duplicate prevention)
        Assert.Equal(500m, result.Balance);
    }
}
