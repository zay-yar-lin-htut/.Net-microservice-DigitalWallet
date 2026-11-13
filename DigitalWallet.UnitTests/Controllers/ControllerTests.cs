using System.Security.Claims;
using IdentityService.Contracts;
using IdentityService.Controllers;
using IdentityService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using TransferService.Contracts;
using TransferService.Controllers;
using TransferService.Services;
using WalletService.Contracts;
using WalletService.Controllers;
using WalletService.Entities;
using WalletService.Services;
using Xunit;

namespace DigitalWallet.UnitTests.Controllers;

public class ControllerTests
{

    [Fact]
    public void IdentityController_Status_ReturnsOk()
    {
        // Arrange
        var serviceMock = new Mock<IIdentityService>();
        var controller = new IdentityController(serviceMock.Object);

        // Act
        var result = controller.Status() as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task IdentityController_Register_ReturnsAccepted()
    {
        // Arrange
        var serviceMock = new Mock<IIdentityService>();
        var requestId = Guid.NewGuid();
        var response = new RegistrationResponse(requestId, "user@test.com", "Pending", DateTime.UtcNow);
        serviceMock.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new IdentityController(serviceMock.Object);
        var request = new RegisterRequest("user@test.com", "Password1!");

        // Act
        var actionResult = await controller.Register(request, CancellationToken.None);

        // Assert
        var result = Assert.IsType<AcceptedResult>(actionResult.Result);
        Assert.Equal(202, result.StatusCode);
        Assert.Equal(response, result.Value);
    }

    [Fact]
    public async Task IdentityController_RegistrationStatus_WhenFound_ReturnsOk()
    {
        // Arrange
        var serviceMock = new Mock<IIdentityService>();
        var requestId = Guid.NewGuid();
        var response = new RegistrationResponse(requestId, "user@test.com", "Completed", DateTime.UtcNow);
        serviceMock.Setup(s => s.GetRegistrationStatusAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new IdentityController(serviceMock.Object);

        // Act
        var actionResult = await controller.RegistrationStatus(requestId, CancellationToken.None);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(response, result.Value);
    }

    [Fact]
    public async Task IdentityController_RegistrationStatus_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var serviceMock = new Mock<IIdentityService>();
        serviceMock.Setup(s => s.GetRegistrationStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegistrationResponse?)null);

        var controller = new IdentityController(serviceMock.Object);

        // Act
        var actionResult = await controller.RegistrationStatus(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(actionResult.Result);
    }

    [Fact]
    public async Task IdentityController_Login_ReturnsOk()
    {
        // Arrange
        var serviceMock = new Mock<IIdentityService>();
        var loginResponse = new LoginResponse("token123", "Bearer", DateTime.UtcNow.AddMinutes(15));
        serviceMock.Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResponse);

        var controller = new IdentityController(serviceMock.Object);
        var request = new LoginRequest("user@test.com", "Password1!");

        // Act
        var actionResult = await controller.Login(request, CancellationToken.None);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(loginResponse, result.Value);
    }

    [Fact]
    public async Task WalletController_Create_ReturnsCreatedWithWallet()
    {
        // Arrange
        var serviceMock = new Mock<IWalletService>();
        var configMock = new Mock<IConfiguration>();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { Id = Guid.NewGuid(), UserId = userId, Balance = 0m, Currency = "MMK" };

        serviceMock.Setup(s => s.CreateAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        var controller = new WalletController(serviceMock.Object, configMock.Object);
        SetupControllerUser(controller, userId);

        // Act
        var actionResult = await controller.Create(CancellationToken.None);

        // Assert
        var result = Assert.IsType<CreatedResult>(actionResult.Result);
        Assert.Equal(201, result.StatusCode);
    }

    [Fact]
    public async Task WalletController_GetMine_WhenFound_ReturnsOk()
    {
        // Arrange
        var serviceMock = new Mock<IWalletService>();
        var configMock = new Mock<IConfiguration>();
        var userId = Guid.NewGuid();
        var wallet = new Wallet { Id = Guid.NewGuid(), UserId = userId, Balance = 500m };

        serviceMock.Setup(s => s.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(wallet);

        var controller = new WalletController(serviceMock.Object, configMock.Object);
        SetupControllerUser(controller, userId);

        // Act
        var actionResult = await controller.GetMine(CancellationToken.None);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        var res = Assert.IsType<WalletResponse>(result.Value);
        Assert.Equal(500m, res.Balance);
    }

    [Fact]
    public async Task WalletController_GetMine_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var serviceMock = new Mock<IWalletService>();
        var configMock = new Mock<IConfiguration>();
        var userId = Guid.NewGuid();

        serviceMock.Setup(s => s.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wallet?)null);

        var controller = new WalletController(serviceMock.Object, configMock.Object);
        SetupControllerUser(controller, userId);

        // Act
        var actionResult = await controller.GetMine(CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task TransferController_Create_WhenMissingIdempotencyKey_ReturnsBadRequest()
    {
        // Arrange
        var serviceMock = new Mock<ITransferService>();
        var controller = new TransferController(serviceMock.Object);
        SetupControllerUser(controller, Guid.NewGuid());

        var request = new CreateTransferRequest { ReceiverUserId = Guid.NewGuid(), Amount = 100m };

        // Act
        var actionResult = await controller.Create(request, CancellationToken.None);

        // Assert
        var result = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task TransferController_Create_WithIdempotencyKey_ReturnsAccepted()
    {
        // Arrange
        var serviceMock = new Mock<ITransferService>();
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var key = "test-idem-key";

        var response = new TransferResponse(
            Guid.NewGuid(), senderId, receiverId, 100m, "MMK", "Pending", null, DateTime.UtcNow, null, null);

        serviceMock.Setup(s => s.CreateAsync(senderId, It.IsAny<CreateTransferRequest>(), key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new TransferController(serviceMock.Object);
        SetupControllerUser(controller, senderId);
        controller.ControllerContext.HttpContext.Request.Headers["Idempotency-Key"] = key;

        var request = new CreateTransferRequest { ReceiverUserId = receiverId, Amount = 100m };

        // Act
        var actionResult = await controller.Create(request, CancellationToken.None);

        // Assert
        var result = Assert.IsType<AcceptedResult>(actionResult.Result);
        Assert.Equal(202, result.StatusCode);
        Assert.Equal(response, result.Value);
    }

    [Fact]
    public async Task TransferController_Get_WhenFound_ReturnsOk()
    {
        // Arrange
        var serviceMock = new Mock<ITransferService>();
        var transferId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var response = new TransferResponse(
            transferId, userId, Guid.NewGuid(), 100m, "MMK", "Completed", null, DateTime.UtcNow, DateTime.UtcNow, null);

        serviceMock.Setup(s => s.GetAsync(transferId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new TransferController(serviceMock.Object);
        SetupControllerUser(controller, userId);

        // Act
        var actionResult = await controller.Get(transferId, CancellationToken.None);

        // Assert
        var result = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(response, result.Value);
    }

    [Fact]
    public async Task TransferController_Get_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var serviceMock = new Mock<ITransferService>();
        var userId = Guid.NewGuid();

        serviceMock.Setup(s => s.GetAsync(It.IsAny<Guid>(), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransferResponse?)null);

        var controller = new TransferController(serviceMock.Object);
        SetupControllerUser(controller, userId);

        // Act
        var actionResult = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(actionResult.Result);
    }

    private static void SetupControllerUser(ControllerBase controller, Guid userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }
}
