using Product.Contracts.Users.PaymentsMethods.Card;

namespace Product.Contracts.Users.PaymentsMethods;

public partial class CreatePaymentMethodRequest
{
    public CardPayer? Payer { get; set; }

    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public int? CardExpMonth { get; set; }
    public int? CardExpYear { get; set; }
    public string? CardHolderName { get; set; }
    public string? MpCustomerId { get; set; }
    public string? MpCardId { get; set; }
    public string? MpPaymentMethodId { get; set; }
}
