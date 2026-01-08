namespace Pruduct.Contracts.Wallet;

public class DepositListResponse
{
    public IReadOnlyCollection<DepositListItem> Items { get; set; } =
        Array.Empty<DepositListItem>();
    public string? NextCursor { get; set; }
}
