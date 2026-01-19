namespace Product.Contracts.Wallet;

public class WithdrawalListItem
{
    public Guid Id { get; set; }
    public string Status { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public DateTimeOffset RequestedAt { get; set; }
}
