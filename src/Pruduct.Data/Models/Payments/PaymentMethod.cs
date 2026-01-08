using Pruduct.Common.Entities;
using Pruduct.Common.Enums;

namespace Pruduct.Data.Models;

public class PaymentMethod : Entity<Guid>
{
    public Guid UserId { get; set; }
    public PaymentMethodType Type { get; set; }
    public bool IsDefault { get; set; }

    public string? PixKey { get; set; }

    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public int? CardExpMonth { get; set; }
    public int? CardExpYear { get; set; }
    public string? CardHolderName { get; set; }

    public string? BankCode { get; set; }
    public string? BankName { get; set; }
    public string? Agency { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountDigit { get; set; }
    public string? AccountType { get; set; }

    public User? User { get; set; }
}
