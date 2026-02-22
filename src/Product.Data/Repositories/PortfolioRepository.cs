using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Markets;
using Product.Data.Models.Portfolio;

namespace Product.Data.Repositories;

public class PortfolioRepository(AppDbContext db) : IPortfolioRepository
{
    public async Task<List<Position>> GetUserPositionsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await db.Positions.Where(x => x.UserId == userId).ToListAsync(ct);
    }

    public async Task<(List<Position> Items, int Total)> GetUserPositionsPageAsync(
        Guid userId,
        string? status,
        string? side,
        string? search,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = db.Positions.Where(x => x.UserId == userId);

        var normalizedSide = side?.Trim().ToLowerInvariant();
        if (normalizedSide is "yes" or "no")
        {
            query = query.Where(x => x.Side.ToLower() == normalizedSide);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"{search.Trim()}%";
            query = query.Where(x =>
                db.Markets.Any(m => m.Id == x.MarketId && EF.Functions.ILike(m.Title, like))
            );
        }

        var normalizedCategory = category?.Trim();
        var categoryIsAll =
            string.IsNullOrWhiteSpace(normalizedCategory)
            || string.Equals(normalizedCategory, "todas", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCategory, "all", StringComparison.OrdinalIgnoreCase);
        if (!categoryIsAll)
        {
            query = query.Where(x =>
                db.Markets.Any(m =>
                    m.Id == x.MarketId
                    && m.Category != null
                    && EF.Functions.ILike(m.Category, normalizedCategory!)
                )
                || db.MarketCategories.Any(mc =>
                    mc.MarketId == x.MarketId
                    && mc.Category != null
                    && (
                        EF.Functions.ILike(mc.Category.Name, normalizedCategory!)
                        || (mc.Category.Slug != null
                            && EF.Functions.ILike(mc.Category.Slug, normalizedCategory!))
                    )
                )
            );
        }

        var normalizedStatus = status?.Trim().ToLowerInvariant();
        if (normalizedStatus == "active")
        {
            query = query.Where(x =>
                x.Contracts > 0
                && x.Status.ToLower() == "open"
                && db.Markets.Any(m => m.Id == x.MarketId && m.Status.ToLower() == "open")
            );
        }
        else if (normalizedStatus == "closed")
        {
            query = query.Where(x =>
                x.Contracts <= 0
                || x.Status.ToLower() != "open"
                || db.Markets.Any(m => m.Id == x.MarketId && m.Status.ToLower() != "open")
            );
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.UpdatedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Dictionary<Guid, Market>> GetMarketsByIdsMapAsync(
        IEnumerable<Guid> marketIds,
        CancellationToken ct = default
    )
    {
        var ids = marketIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, Market>();
        }

        var markets = await db.Markets.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        return markets.ToDictionary(x => x.Id, x => x);
    }

    public async Task<(List<PositionFill> Items, int Total)> GetUserPositionFillsPageAsync(
        Guid userId,
        string? category,
        Guid? marketId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = db.PositionFills.Where(x => x.UserId == userId);

        if (marketId.HasValue)
        {
            query = query.Where(x => x.MarketId == marketId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var term = category.Trim();
            query = query.Where(x =>
                db.MarketCategories.Any(mc =>
                    mc.MarketId == x.MarketId
                    && mc.Category != null
                    && (
                        EF.Functions.ILike(mc.Category.Name, term)
                        || (mc.Category.Slug != null && EF.Functions.ILike(mc.Category.Slug, term))
                    )
                )
            );
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddPortfolioSnapshotAsync(
        PortfolioSnapshot snapshot,
        CancellationToken ct = default
    )
    {
        db.PortfolioSnapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);
    }
}
