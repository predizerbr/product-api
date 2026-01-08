namespace Pruduct.Contracts.Wallet;

public class WithdrawalListItem
{
    public Guid Id { get; set; }
    public string Status { get; set; } = default!;
    public long Amount { get; set; }
    public string Currency { get; set; } = default!;
    public DateTimeOffset RequestedAt { get; set; }
}
