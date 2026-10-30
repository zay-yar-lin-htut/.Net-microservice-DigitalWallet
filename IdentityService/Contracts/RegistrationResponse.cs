namespace IdentityService.Contracts;

public sealed record RegistrationResponse(
    Guid RequestId,
    string Email,
    string Status,
    DateTime CreatedAtUtc);
