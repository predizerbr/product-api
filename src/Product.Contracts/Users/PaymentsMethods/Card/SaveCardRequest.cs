namespace Product.Contracts.Users.PaymentsMethods.Card;

public class SaveCardRequest
{
    public CardPayer Payer { get; set; } = default!;
    public string? Token { get; set; } = default!;
    public string? PaymentMethodId { get; set; }
    public string? IssuerId { get; set; }
    public string? DeviceId { get; set; }
    public bool? IsDefault { get; set; }
}
