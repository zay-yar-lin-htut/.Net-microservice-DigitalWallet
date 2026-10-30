using IdentityService.Contracts;

namespace IdentityService.Services;

public interface IIdentityService
{
    Task<UserResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken);

    Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken);
}
