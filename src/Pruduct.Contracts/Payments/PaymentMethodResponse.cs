using System;

namespace Pruduct.Contracts.Payments;

public class PaymentMethodResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

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
}
