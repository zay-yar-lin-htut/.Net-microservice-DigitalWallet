using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TransferService.Contracts;
using TransferService.Data;
using TransferService.Entities;
using TransferService.Services;
using Xunit;

namespace DigitalWallet.UnitTests.Services;

public class TransferServiceTests
{
    private static TransferDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TransferDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new TransferDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_WhenSenderEqualsReceiver_ThrowsSameUserTransferException()
    {
        // Arrange
        using var db = CreateDbContext();
        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<TransferService.Services.TransferService>>();
        var service = new TransferService.Services.TransferService(db, publishMock.Object, loggerMock.Object);

        var userId = Guid.NewGuid();
        var request = new CreateTransferRequest { ReceiverUserId = userId, Amount = 100m, Currency = "MMK" };

        // Act & Assert
        await Assert.ThrowsAsync<SameUserTransferException>(() =>
            service.CreateAsync(userId, request, "key-1", CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_WithValidInput_CreatesTransferAndPublishesCommand()
    {
        // Arrange
        using var db = CreateDbContext();
        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<TransferService.Services.TransferService>>();
        var service = new TransferService.Services.TransferService(db, publishMock.Object, loggerMock.Object);

        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var request = new CreateTransferRequest { ReceiverUserId = receiverId, Amount = 250m, Currency = "mmk" };
        var idempotencyKey = "unique-key-123";

        // Act
        var response = await service.CreateAsync(senderId, request, idempotencyKey, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(senderId, response.SenderUserId);
        Assert.Equal(receiverId, response.ReceiverUserId);
        Assert.Equal(250m, response.Amount);
        Assert.Equal("MMK", response.Currency); // Capitalized
        Assert.Equal("Pending", response.Status);

        var saved = await db.Transfers.SingleOrDefaultAsync(t => t.Id == response.Id);
        Assert.NotNull(saved);
        Assert.Equal(idempotencyKey, saved.IdempotencyKey);

        publishMock.Verify(p => p.Publish(
            It.Is<DigitalWallet.Contracts.StartTransferCommand>(cmd => cmd.TransferId == response.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingIdenticalIdempotencyKey_ReturnsExistingTransfer()
    {
        // Arrange
        using var db = CreateDbContext();
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var key = "existing-key-456";

        var existingTransfer = new Transfer
        {
            SenderUserId = senderId,
            ReceiverUserId = receiverId,
            Amount = 500m,
            Currency = "MMK",
            IdempotencyKey = key,
            Status = "Pending"
        };
        db.Transfers.Add(existingTransfer);
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<TransferService.Services.TransferService>>();
        var service = new TransferService.Services.TransferService(db, publishMock.Object, loggerMock.Object);

        var request = new CreateTransferRequest { ReceiverUserId = receiverId, Amount = 500m, Currency = "MMK" };

        // Act
        var response = await service.CreateAsync(senderId, request, key, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(existingTransfer.Id, response.Id);
        // Should NOT publish a new command
        publishMock.Verify(p => p.Publish(
            It.IsAny<DigitalWallet.Contracts.StartTransferCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithExistingKeyButDifferentAmount_ThrowsTransferOperationException()
    {
        // Arrange
        using var db = CreateDbContext();
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var key = "mismatched-key";

        db.Transfers.Add(new Transfer
        {
            SenderUserId = senderId,
            ReceiverUserId = receiverId,
            Amount = 500m,
            Currency = "MMK",
            IdempotencyKey = key
        });
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<TransferService.Services.TransferService>>();
        var service = new TransferService.Services.TransferService(db, publishMock.Object, loggerMock.Object);

        var request = new CreateTransferRequest { ReceiverUserId = receiverId, Amount = 999m, Currency = "MMK" };

        // Act & Assert
        await Assert.ThrowsAsync<TransferOperationException>(() =>
            service.CreateAsync(senderId, request, key, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_WhenUserIsSenderOrReceiver_ReturnsTransfer()
    {
        // Arrange
        using var db = CreateDbContext();
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var transfer = new Transfer
        {
            SenderUserId = senderId,
            ReceiverUserId = receiverId,
            Amount = 100m,
            Currency = "MMK",
            IdempotencyKey = "get-test-key"
        };
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<TransferService.Services.TransferService>>();
        var service = new TransferService.Services.TransferService(db, publishMock.Object, loggerMock.Object);

        // Act
        var asSender = await service.GetAsync(transfer.Id, senderId, CancellationToken.None);
        var asReceiver = await service.GetAsync(transfer.Id, receiverId, CancellationToken.None);

        // Assert
        Assert.NotNull(asSender);
        Assert.Equal(transfer.Id, asSender.Id);
        Assert.NotNull(asReceiver);
        Assert.Equal(transfer.Id, asReceiver.Id);
    }

    [Fact]
    public async Task GetAsync_WhenUserIsNotParticipant_ReturnsNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();

        var transfer = new Transfer
        {
            SenderUserId = senderId,
            ReceiverUserId = receiverId,
            Amount = 100m,
            Currency = "MMK",
            IdempotencyKey = "stranger-test"
        };
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<TransferService.Services.TransferService>>();
        var service = new TransferService.Services.TransferService(db, publishMock.Object, loggerMock.Object);

        // Act
        var result = await service.GetAsync(transfer.Id, strangerId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_PaginatesAndFiltersCorrectly()
    {
        // Arrange
        using var db = CreateDbContext();
        var targetUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();

        for (int i = 0; i < 15; i++)
        {
            db.Transfers.Add(new Transfer
            {
                SenderUserId = targetUser,
                ReceiverUserId = otherUser,
                Amount = 10m * (i + 1),
                Currency = "MMK",
                IdempotencyKey = $"page-key-{i}",
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<TransferService.Services.TransferService>>();
        var service = new TransferService.Services.TransferService(db, publishMock.Object, loggerMock.Object);

        // Act (Page 1 with pageSize 5)
        var page1 = await service.GetByUserIdAsync(targetUser, page: 1, pageSize: 5, CancellationToken.None);

        // Assert
        Assert.Equal(15, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(1, page1.Page);
        Assert.Equal(5, page1.PageSize);
    }
}
