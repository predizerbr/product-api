namespace Product.Contracts.Wallet;

public class WalletSummaryResponse
{
    public string Currency { get; set; } = "BRL";
    public decimal TotalDeposited { get; set; }
    public decimal TotalWithdrawn { get; set; }
    public decimal TotalBought { get; set; }
}
