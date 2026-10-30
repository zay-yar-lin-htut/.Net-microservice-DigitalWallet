namespace WalletService.Contracts;

public sealed record WalletOperationResponse(
    Guid OperationId,
    Guid UserId,
    string OperationType,
    decimal Amount,
    string Status,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
