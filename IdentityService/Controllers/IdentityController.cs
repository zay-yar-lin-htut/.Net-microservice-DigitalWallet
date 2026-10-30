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
    public async Task<ActionResult<UserResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = await identityService.RegisterAsync(request, cancellationToken);
        return Created($"/api/identity/users/{user.Id}", user);
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
