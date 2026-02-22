using Product.Common.Entities;

namespace Product.Data.Models.Portfolio;

public class PositionFill : Entity<Guid>
{
    public Guid PositionId { get; set; }
    public Guid UserId { get; set; }
    public Guid MarketId { get; set; }
    public string Side { get; set; } = "yes"; // yes|no
    public string Type { get; set; } = "BUY"; // BUY|SELL
    public int Contracts { get; set; }
    public decimal Price { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Source { get; set; } = "ORDER"; // ORDER|ADMIN|ADJUSTMENT
    public Guid? OrderId { get; set; }
    public string? IdempotencyKey { get; set; }
}
