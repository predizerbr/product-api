namespace Product.Contracts.Wallet;

public class CreateDepositResponse
{
    public Guid PaymentIntentId { get; set; }
    public string Provider { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
