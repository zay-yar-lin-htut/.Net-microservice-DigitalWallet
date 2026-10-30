namespace LedgerService.Entities;

public sealed class LedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string ReferenceId { get; set; } = string.Empty;

    public string EntryType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "MMK";

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
