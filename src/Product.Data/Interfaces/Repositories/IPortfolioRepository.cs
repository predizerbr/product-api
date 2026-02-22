using Product.Data.Models.Markets;
using Product.Data.Models.Portfolio;

namespace Product.Data.Interfaces.Repositories;

public interface IPortfolioRepository
{
    Task<List<Position>> GetUserPositionsAsync(Guid userId, CancellationToken ct = default);

    Task<(List<Position> Items, int Total)> GetUserPositionsPageAsync(
        Guid userId,
        string? status,
        string? side,
        string? search,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task<Dictionary<Guid, Market>> GetMarketsByIdsMapAsync(
        IEnumerable<Guid> marketIds,
        CancellationToken ct = default
    );

    Task<(List<PositionFill> Items, int Total)> GetUserPositionFillsPageAsync(
        Guid userId,
        string? category,
        Guid? marketId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task AddPortfolioSnapshotAsync(PortfolioSnapshot snapshot, CancellationToken ct = default);
}
