namespace Pruduct.Contracts.Wallet;

public class WalletBalanceResponse
{
    public string Currency { get; set; } = default!;
    public long Balance { get; set; }
    public long Available { get; set; }
}
