using IdentityService.Contracts;
using IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/identity")]
public sealed class IdentityController(IIdentityService identityService) : ControllerBase
{
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status() => Ok(new
    {
        service = "Identity Service",
        status = "Healthy"
    });

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegistrationResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = await identityService.RegisterAsync(request, cancellationToken);
        return Accepted($"/api/identity/registration-status/{user.RequestId}", user);
    }

    [HttpGet("registration-status/{requestId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<RegistrationResponse>> RegistrationStatus(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var registration = await identityService.GetRegistrationStatusAsync(
            requestId,
            cancellationToken);

        return registration is null
            ? NotFound()
            : Ok(registration);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await identityService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }
}
