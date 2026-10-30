namespace LedgerService.Contracts;

public sealed record LedgerEntryResponse(
    Guid Id,
    Guid UserId,
    string ReferenceId,
    string EntryType,
    decimal Amount,
    string Currency,
    string? Description,
    DateTime CreatedAtUtc);
