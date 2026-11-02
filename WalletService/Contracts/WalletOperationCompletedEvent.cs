namespace DigitalWallet.Contracts;

public sealed record WalletOperationCompletedEvent(
    Guid OperationId,
    Guid UserId,
    string OperationType,
    decimal Amount,
    string Currency,
    DateTime CompletedAtUtc);
