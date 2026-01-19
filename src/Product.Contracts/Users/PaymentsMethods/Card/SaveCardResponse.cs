namespace Product.Contracts.Users.PaymentsMethods.Card;

public class SaveCardResponse
{
    public string MpCustomerId { get; set; } = default!;
    public string MpCardId { get; set; } = default!;
    public string? MpPaymentMethodId { get; set; }
    public string? CardLast4 { get; set; }
    public int? CardExpMonth { get; set; }
    public int? CardExpYear { get; set; }
    public string? CardHolderName { get; set; }
    public string? CardBrand { get; set; }
}
