namespace Product.Contracts.Wallet;

public class WalletBalanceResponse
{
    public string Currency { get; set; } = default!;
    public decimal Balance { get; set; }
    public decimal Available { get; set; }
}
