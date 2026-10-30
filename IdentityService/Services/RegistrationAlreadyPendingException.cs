namespace IdentityService.Services;

public sealed class RegistrationAlreadyPendingException()
    : Exception("Registration is already pending.");
