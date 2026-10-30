using IdentityService.Contracts;

namespace IdentityService.Services;

public interface IIdentityService
{
    Task<RegistrationResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken);

    Task<RegistrationResponse?> GetRegistrationStatusAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken);
}
