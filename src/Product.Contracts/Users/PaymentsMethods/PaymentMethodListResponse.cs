namespace Product.Contracts.Users.PaymentsMethods;

public class PaymentMethodListResponse
{
    public IReadOnlyCollection<PaymentMethodResponse> Items { get; set; } =
        Array.Empty<PaymentMethodResponse>();
}
