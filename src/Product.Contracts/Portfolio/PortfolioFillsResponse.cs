namespace Product.Contracts.Portfolio;

public class PortfolioFillsResponse
{
    public IReadOnlyCollection<PortfolioFillItem> Items { get; set; } =
        Array.Empty<PortfolioFillItem>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
