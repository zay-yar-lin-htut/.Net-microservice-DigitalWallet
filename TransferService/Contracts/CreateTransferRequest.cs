using System.ComponentModel.DataAnnotations;

namespace TransferService.Contracts;

public sealed class CreateTransferRequest
{
    [Required]
    public Guid ReceiverUserId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "MMK";
}
