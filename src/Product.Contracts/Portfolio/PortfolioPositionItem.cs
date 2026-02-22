namespace Product.Contracts.Portfolio;

public class PortfolioPositionItem
{
    public Guid PositionId { get; set; }
    public Guid MarketId { get; set; }
    public string MarketTitle { get; set; } = string.Empty;
    public string MarketStatus { get; set; } = "open";
    public string Side { get; set; } = "YES";
    public int Contracts { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal PotentialPnl { get; set; }
    public decimal RealizedPnl { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
