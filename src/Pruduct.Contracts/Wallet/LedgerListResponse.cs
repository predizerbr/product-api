namespace Pruduct.Contracts.Wallet;

public class LedgerListResponse
{
    public IReadOnlyCollection<LedgerEntryResponse> Entries { get; set; } =
        Array.Empty<LedgerEntryResponse>();
    public string? NextCursor { get; set; }
}
