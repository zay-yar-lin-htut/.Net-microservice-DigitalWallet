using System.ComponentModel.DataAnnotations;

namespace IdentityService.Contracts;

public sealed record LoginRequest(
    [Required][EmailAddress]
    string Email,
    [Required][MinLength(1)]
    string Password
);
