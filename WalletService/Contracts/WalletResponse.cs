namespace WalletService.Contracts;

public sealed record WalletResponse(
    Guid Id,
    Guid UserId,
    decimal Balance,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
