using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LedgerService.Contracts;
using LedgerService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LedgerService.Controllers;

[ApiController]
[Route("api/ledger")]
public sealed class LedgerController(
    ILedgerService ledgerService,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status() => Ok(new
    {
        service = "Ledger Service",
        status = "Healthy"
    });

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<LedgerEntryResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        var entries = await ledgerService.GetByUserIdAsync(
            GetUserId(),
            cancellationToken);

        return Ok(entries);
    }

    [HttpPost("entries")]
    [Authorize]
    public async Task<ActionResult<LedgerEntryResponse>> Create(
        CreateLedgerEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await ledgerService.CreateAsync(
            GetUserId(),
            request,
            cancellationToken);

        return Created($"/api/ledger/entries/{entry.Id}", entry);
    }

    [HttpPost("internal/entries")]
    [AllowAnonymous]
    public async Task<ActionResult<LedgerEntryResponse>> CreateInternal(
        InternalLedgerEntryRequest request,
        CancellationToken cancellationToken)
    {
        EnsureInternalAccess();

        var entry = await ledgerService.CreateAsync(
            request.UserId,
            new CreateLedgerEntryRequest
            {
                ReferenceId = request.ReferenceId,
                EntryType = request.EntryType,
                Amount = request.Amount,
                Currency = request.Currency,
                Description = request.Description
            },
            cancellationToken);

        return Created($"/api/ledger/entries/{entry.Id}", entry);
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
}
