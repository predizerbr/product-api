using Product.Common.Entities;

namespace Product.Data.Models.Portfolio;

public class PortfolioSnapshot : Entity<Guid>
{
    public Guid UserId { get; set; }
    public DateTimeOffset AsOf { get; set; } = DateTimeOffset.UtcNow;
    public int ActivePositions { get; set; }
    public decimal TotalInvestedActive { get; set; }
    public decimal TotalInvestedAllTime { get; set; }
    public decimal RealizedPnlAllTime { get; set; }
    public decimal PotentialPnlActive { get; set; }
    public int ClosedMarkets { get; set; }
    public int Wins { get; set; }
    public decimal AccuracyRate { get; set; }
}
