using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransferService.Contracts;
using TransferService.Services;

namespace TransferService.Controllers;

[ApiController]
[Route("api/transfer")]
public sealed class TransferController(ITransferService transferService) : ControllerBase
{
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status() => Ok(new
    {
        service = "Transfer Service",
        status = "Healthy"
    });

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<TransferResponse>> Create(
        CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var transfer = await transferService.CreateAsync(
            GetUserId(),
            request,
            cancellationToken);

        return transfer.Status == "Completed"
            ? Created($"/api/transfer/{transfer.Id}", transfer)
            : UnprocessableEntity(transfer);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<TransferResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        var transfers = await transferService.GetByUserIdAsync(
            GetUserId(),
            cancellationToken);

        return Ok(transfers);
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
