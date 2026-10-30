namespace TransferService.Entities;

public sealed class Transfer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SenderUserId { get; set; }

    public Guid ReceiverUserId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "MMK";

    public string Status { get; set; } = "Pending";

    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }
}
