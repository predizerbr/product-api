namespace Pruduct.Contracts.Wallet;

public class WithdrawalListResponse
{
    public IReadOnlyCollection<WithdrawalListItem> Items { get; set; } =
        Array.Empty<WithdrawalListItem>();
    public string? NextCursor { get; set; }
}
