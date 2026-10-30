namespace WalletService.Entities;

public sealed class WalletOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? ReferenceId { get; set; }

    public Guid UserId { get; set; }

    public string OperationType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = "Pending";

    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }
}
