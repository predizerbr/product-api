namespace Product.Contracts.Portfolio;

public class PortfolioSummaryResponse
{
    public string Scope { get; set; } = "active";
    public int ActivePositions { get; set; }
    public decimal TotalInvested { get; set; }
    public decimal TotalInvestedActive { get; set; }
    public decimal TotalInvestedAllTime { get; set; }
    public decimal RealizedPnlAllTime { get; set; }
    public decimal PotentialPnlActive { get; set; }
    public int ClosedMarkets { get; set; }
    public int Wins { get; set; }
    public decimal AccuracyRate { get; set; }
}
