using System.Security.Claims;
using IdentityService.Contracts;
using IdentityService.Data;
using IdentityService.Entities;
using IdentityService.Services;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace DigitalWallet.UnitTests.Services;

public class IdentityServiceTests
{
    private const string ValidSecret = "SuperSecretKeyForJwtTokenGenerationMustBeLongEnough12345";
    private const string ValidIssuer = "DigitalWallet.Identity";
    private const string ValidAudience = "DigitalWallet.Services";

    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private static Mock<IConfiguration> CreateValidConfigurationMock()
    {
        var mock = new Mock<IConfiguration>();
        mock.Setup(c => c["Jwt:Issuer"]).Returns(ValidIssuer);
        mock.Setup(c => c["Jwt:Audience"]).Returns(ValidAudience);
        mock.Setup(c => c["Jwt:Secret"]).Returns(ValidSecret);
        return mock;
    }

    [Fact]
    public async Task RegisterAsync_WithValidRequest_CreatesRegistrationAndPublishesCommand()
    {
        // Arrange
        using var db = CreateDbContext();
        var hasherMock = new Mock<IPasswordHasher<User>>();
        hasherMock.Setup(h => h.HashPassword(It.IsAny<User>(), "P@ssword123"))
            .Returns("hashed_password_123");

        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);
        var request = new RegisterRequest("test@example.com", "P@ssword123");

        // Act
        var response = await service.RegisterAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("test@example.com", response.Email);
        Assert.Equal("Pending", response.Status);

        var saved = await db.RegistrationRequests.SingleOrDefaultAsync(r => r.Id == response.RequestId);
        Assert.NotNull(saved);
        Assert.Equal("test@example.com", saved.Email);
        Assert.Equal("hashed_password_123", saved.PasswordHash);

        publishMock.Verify(p => p.Publish(
            It.Is<DigitalWallet.Contracts.RegisterUserCommand>(cmd => cmd.RequestId == response.RequestId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmailToLowerCaseAndTrimmed()
    {
        // Arrange
        using var db = CreateDbContext();
        var hasherMock = new Mock<IPasswordHasher<User>>();
        hasherMock.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed_pw");

        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);
        var request = new RegisterRequest("  MyUser@Domain.COM  ", "SecretPassword1");

        // Act
        var response = await service.RegisterAsync(request, CancellationToken.None);

        // Assert
        Assert.Equal("myuser@domain.com", response.Email);
        var saved = await db.RegistrationRequests.SingleOrDefaultAsync(r => r.Id == response.RequestId);
        Assert.NotNull(saved);
        Assert.Equal("myuser@domain.com", saved.Email);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ThrowsEmailAlreadyExistsException()
    {
        // Arrange
        using var db = CreateDbContext();
        db.Users.Add(new User { Email = "existing@example.com", PasswordHash = "hash" });
        await db.SaveChangesAsync();

        var hasherMock = new Mock<IPasswordHasher<User>>();
        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);
        var request = new RegisterRequest("Existing@example.com", "Password123");

        // Act & Assert
        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() =>
            service.RegisterAsync(request, CancellationToken.None));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Processing")]
    public async Task RegisterAsync_WhenPendingOrProcessingRegistrationExists_ThrowsRegistrationAlreadyPendingException(string status)
    {
        // Arrange
        using var db = CreateDbContext();
        db.RegistrationRequests.Add(new RegistrationRequest
        {
            Email = "pending@example.com",
            PasswordHash = "hash",
            Status = status
        });
        await db.SaveChangesAsync();

        var hasherMock = new Mock<IPasswordHasher<User>>();
        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);
        var request = new RegisterRequest("pending@example.com", "Password123");

        // Act & Assert
        await Assert.ThrowsAsync<RegistrationAlreadyPendingException>(() =>
            service.RegisterAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetRegistrationStatusAsync_WhenFound_ReturnsResponse()
    {
        // Arrange
        using var db = CreateDbContext();
        var reg = new RegistrationRequest
        {
            Email = "status@example.com",
            Status = "Completed",
            PasswordHash = "hash"
        };
        db.RegistrationRequests.Add(reg);
        await db.SaveChangesAsync();

        var hasherMock = new Mock<IPasswordHasher<User>>();
        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);

        // Act
        var result = await service.GetRegistrationStatusAsync(reg.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(reg.Id, result.RequestId);
        Assert.Equal("status@example.com", result.Email);
        Assert.Equal("Completed", result.Status);
    }

    [Fact]
    public async Task GetRegistrationStatusAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        using var db = CreateDbContext();
        var hasherMock = new Mock<IPasswordHasher<User>>();
        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);

        // Act
        var result = await service.GetRegistrationStatusAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_WhenUserNotFound_ThrowsInvalidCredentialsException()
    {
        // Arrange
        using var db = CreateDbContext();
        var hasherMock = new Mock<IPasswordHasher<User>>();
        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);
        var request = new LoginRequest("nonexistent@example.com", "Password123");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.LoginAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WhenPasswordFails_ThrowsInvalidCredentialsException()
    {
        // Arrange
        using var db = CreateDbContext();
        var user = new User { Email = "valid@example.com", PasswordHash = "hashed_pw" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var hasherMock = new Mock<IPasswordHasher<User>>();
        hasherMock.Setup(h => h.VerifyHashedPassword(user, "hashed_pw", "WrongPassword"))
            .Returns(PasswordVerificationResult.Failed);

        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);
        var request = new LoginRequest("valid@example.com", "WrongPassword");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            service.LoginAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WhenCredentialsValid_ReturnsValidJwtToken()
    {
        // Arrange
        using var db = CreateDbContext();
        var user = new User { Email = "valid@example.com", PasswordHash = "hashed_pw" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var hasherMock = new Mock<IPasswordHasher<User>>();
        hasherMock.Setup(h => h.VerifyHashedPassword(user, "hashed_pw", "CorrectPassword"))
            .Returns(PasswordVerificationResult.Success);

        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);
        var request = new LoginRequest("valid@example.com", "CorrectPassword");

        // Act
        var result = await service.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.True(result.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_WhenRehashNeeded_UpdatesPasswordHashAndSucceeds()
    {
        // Arrange
        using var db = CreateDbContext();
        var user = new User { Email = "rehash@example.com", PasswordHash = "old_hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var hasherMock = new Mock<IPasswordHasher<User>>();
        hasherMock.Setup(h => h.VerifyHashedPassword(user, "old_hash", "Password123"))
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);
        hasherMock.Setup(h => h.HashPassword(user, "Password123"))
            .Returns("new_upgraded_hash");

        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = CreateValidConfigurationMock();

        var service = new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object);
        var request = new LoginRequest("rehash@example.com", "Password123");

        // Act
        var result = await service.LoginAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var updatedUser = await db.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal("new_upgraded_hash", updatedUser.PasswordHash);
    }

    [Fact]
    public void Constructor_WhenJwtSecretMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateDbContext();
        var hasherMock = new Mock<IPasswordHasher<User>>();
        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:Issuer"]).Returns(ValidIssuer);
        configMock.Setup(c => c["Jwt:Audience"]).Returns(ValidAudience);
        configMock.Setup(c => c["Jwt:Secret"]).Returns((string?)null);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object));
    }

    [Fact]
    public void Constructor_WhenJwtSecretTooShort_ThrowsInvalidOperationException()
    {
        // Arrange
        using var db = CreateDbContext();
        var hasherMock = new Mock<IPasswordHasher<User>>();
        var publishMock = new Mock<IPublishEndpoint>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Jwt:Issuer"]).Returns(ValidIssuer);
        configMock.Setup(c => c["Jwt:Audience"]).Returns(ValidAudience);
        configMock.Setup(c => c["Jwt:Secret"]).Returns("short-secret-key");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new IdentityService.Services.IdentityService(db, hasherMock.Object, configMock.Object, publishMock.Object));
        Assert.Contains("at least 32 bytes", ex.Message);
    }
}
