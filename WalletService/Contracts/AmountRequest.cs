using System.ComponentModel.DataAnnotations;

namespace WalletService.Contracts;

public sealed class AmountRequest
{
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }
}
