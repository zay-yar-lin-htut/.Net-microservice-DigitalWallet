namespace TransferService.Events;

public sealed record TransferCompletedEvent(
    Guid TransferId,
    Guid SenderUserId,
    Guid ReceiverUserId,
    decimal Amount,
    string Currency,
    DateTime OccurredAtUtc);
