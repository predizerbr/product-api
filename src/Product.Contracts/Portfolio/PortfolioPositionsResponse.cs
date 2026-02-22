namespace Product.Contracts.Portfolio;

public class PortfolioPositionsResponse
{
    public IReadOnlyCollection<PortfolioPositionItem> Items { get; set; } =
        Array.Empty<PortfolioPositionItem>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
