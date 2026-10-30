using System.ComponentModel.DataAnnotations;

namespace IdentityService.Contracts;

public sealed record RegisterRequest(
    [Required][EmailAddress]
    string Email,
    [Required][MinLength(12)]
    string Password
);
