namespace Pruduct.Contracts.Wallet;

public class DepositListItem
{
    public Guid PaymentIntentId { get; set; }
    public string Provider { get; set; } = default!;
    public string Status { get; set; } = default!;
    public long Amount { get; set; }
    public string Currency { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
