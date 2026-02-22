namespace Product.Contracts.Portfolio;

public class PortfolioFillItem
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public Guid MarketId { get; set; }
    public string MarketTitle { get; set; } = string.Empty;
    public string Side { get; set; } = "YES";
    public string Type { get; set; } = "BUY";
    public int Contracts { get; set; }
    public decimal Price { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Source { get; set; } = "ORDER";
    public Guid? OrderId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
