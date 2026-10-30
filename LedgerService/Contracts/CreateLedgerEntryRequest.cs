using System.ComponentModel.DataAnnotations;

namespace LedgerService.Contracts;

public sealed class CreateLedgerEntryRequest
{
    [Required]
    [StringLength(100)]
    public string ReferenceId { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Credit|Debit)$")]
    public string EntryType { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "MMK";

    [StringLength(500)]
    public string? Description { get; set; }
}
