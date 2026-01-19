using Product.Contracts.Users.PaymentsMethods;

namespace Product.Contracts.Users.PaymentsMethods.Pix;

public sealed class CreatePixRequest
{
    public string OrderId { get; set; } = default!;
    public decimal Amount { get; set; }
    public PixPayer Payer { get; set; } = new();
    public string? DeviceId { get; set; }
    public string Description { get; set; } = default!;
    public string BuyerEmail { get; set; } = default!;
    public int? ExpirationMinutes { get; set; }
}

public sealed class PixPayer
{
    public string Email { get; set; } = default!;
    public Identification? Identification { get; set; }
}
