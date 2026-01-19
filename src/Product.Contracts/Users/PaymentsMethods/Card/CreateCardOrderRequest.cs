namespace Product.Contracts.Users.PaymentsMethods.Card;

public sealed class CreateCardOrderRequest
{
    public string OrderId { get; set; } = default!;
    public decimal Amount { get; set; }
    public string? Token { get; set; } = default!;
    public int Installments { get; set; } = 1;
    public string PaymentMethodId { get; set; } = default!;
    public string? PaymentType { get; set; } // credit_card | debit_card
    public string? IssuerId { get; set; }
    // CVV for saved-card payments when no token is provided
    public string? SecurityCode { get; set; }
    public CardPayer Payer { get; set; } = new();
    public string? DeviceId { get; set; }
    public string? MpCardId { get; set; }
}
