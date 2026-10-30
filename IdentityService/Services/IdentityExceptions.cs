namespace IdentityService.Services;

public sealed class EmailAlreadyExistsException()
    : Exception("Email already exists.");

public sealed class InvalidCredentialsException()
    : Exception("Invalid email or password.");
