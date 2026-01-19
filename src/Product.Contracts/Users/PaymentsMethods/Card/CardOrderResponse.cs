namespace Product.Contracts.Users.PaymentsMethods.Card;

public class CardOrderResponse
{
    public string OrderId { get; set; } = default!;
    public string? ProviderOrderId { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string Status { get; set; } = default!;
    public string? StatusDetail { get; set; }
    public bool IsFinal { get; set; }
    public decimal Amount { get; set; }
}
