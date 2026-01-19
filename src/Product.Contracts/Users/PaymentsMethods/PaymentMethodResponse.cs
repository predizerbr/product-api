namespace Product.Contracts.Users.PaymentsMethods;

public partial class PaymentMethodResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
