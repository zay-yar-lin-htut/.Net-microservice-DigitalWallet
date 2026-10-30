namespace IdentityService.Contracts;

public sealed record UserResponse(
    Guid Id,
    string Email,
    DateTime CreatedAtUtc);
