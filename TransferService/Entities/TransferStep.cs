namespace TransferService.Entities;

public sealed class TransferStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TransferId { get; set; }

    public string StepType { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }
}
