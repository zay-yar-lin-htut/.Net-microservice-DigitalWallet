using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletService.Contracts;
using WalletService.Entities;
using WalletService.Services;

namespace WalletService.Controllers;

[ApiController]
[Route("api/wallet")]
public sealed class WalletController(
    IWalletService walletService,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status() => Ok(new
    {
        service = "Wallet Service",
        status = "Healthy"
    });

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<WalletResponse>> Create(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var wallet = await walletService.CreateAsync(userId, cancellationToken);
        return Created($"/api/wallet/{wallet.Id}", ToResponse(wallet));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<WalletResponse>> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var wallet = await walletService.GetByUserIdAsync(userId, cancellationToken);

        return wallet is null
            ? NotFound(new { message = "Wallet not found." })
            : Ok(ToResponse(wallet));
    }

    [HttpPost("deposit")]
    [Authorize]
    public async Task<ActionResult<WalletResponse>> Deposit(
        AmountRequest request,
        CancellationToken cancellationToken)
    {
        var wallet = await walletService.DepositAsync(
            GetUserId(),
            request.Amount,
            cancellationToken);

        return Ok(ToResponse(wallet));
    }

    [HttpPost("withdraw")]
    [Authorize]
    public async Task<ActionResult<WalletResponse>> Withdraw(
        AmountRequest request,
        CancellationToken cancellationToken)
    {
        var wallet = await walletService.WithdrawAsync(
            GetUserId(),
            request.Amount,
            cancellationToken);

        return Ok(ToResponse(wallet));
    }

    [HttpPost("internal/credit")]
    [AllowAnonymous]
    public async Task<ActionResult<WalletResponse>> InternalCredit(
        InternalWalletOperationRequest request,
        CancellationToken cancellationToken)
    {
        EnsureInternalAccess();
        var wallet = await walletService.CreditInternalAsync(
            request.UserId,
            request.Amount,
            cancellationToken);

        return Ok(ToResponse(wallet));
    }

    [HttpPost("internal/debit")]
    [AllowAnonymous]
    public async Task<ActionResult<WalletResponse>> InternalDebit(
        InternalWalletOperationRequest request,
        CancellationToken cancellationToken)
    {
        EnsureInternalAccess();
        var wallet = await walletService.DebitInternalAsync(
            request.UserId,
            request.Amount,
            cancellationToken);

        return Ok(ToResponse(wallet));
    }

    private void EnsureInternalAccess()
    {
        var expectedKey = configuration["Internal:ApiKey"];
        var actualKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(expectedKey) || actualKey != expectedKey)
        {
            throw new UnauthorizedAccessException("Internal access denied.");
        }
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User identifier is missing.");
    }

    private static WalletResponse ToResponse(Wallet wallet) => new(
        wallet.Id,
        wallet.UserId,
        wallet.Balance,
        wallet.Currency,
        wallet.CreatedAtUtc,
        wallet.UpdatedAtUtc);
}
