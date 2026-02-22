using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Product.Business.Interfaces.Portfolio;
using Product.Business.Interfaces.Results;
using Product.Contracts.Portfolio;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Markets;
using Product.Data.Models.Portfolio;

namespace Product.Business.Services.Portfolio;

public class PortfolioService(IPortfolioRepository portfolioRepository) : IPortfolioService
{
    private readonly IPortfolioRepository _portfolioRepository = portfolioRepository;

    public async Task<ApiResult> GetSummaryApiAsync(
        ClaimsPrincipal principal,
        string? scope,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetSummaryAsync(userId, scope, ct);
        if (!result.Success)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> GetPositionsApiAsync(
        ClaimsPrincipal principal,
        string? status,
        string? side,
        string? search,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetPositionsAsync(
            userId,
            status,
            side,
            search,
            category,
            page,
            pageSize,
            ct
        );
        if (!result.Success)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> GetFillsApiAsync(
        ClaimsPrincipal principal,
        string? category,
        Guid? marketId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetFillsAsync(userId, category, marketId, page, pageSize, ct);
        if (!result.Success)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ServiceResult<PortfolioSummaryResponse>> GetSummaryAsync(
        Guid userId,
        string? scope,
        CancellationToken ct = default
    )
    {
        var positions = await _portfolioRepository.GetUserPositionsAsync(userId, ct);
        var markets = await _portfolioRepository.GetMarketsByIdsMapAsync(
            positions.Select(x => x.MarketId),
            ct
        );

        var rows = positions
            .Select(p =>
            {
                markets.TryGetValue(p.MarketId, out var market);
                return new PortfolioPositionRow(p, market);
            })
            .ToList();

        var activeRows = rows.Where(x => IsActive(x.Position, x.Market)).ToList();
        var closedRows = rows.Where(x => !IsActive(x.Position, x.Market)).ToList();

        var totalInvestedActive = activeRows.Sum(x => x.Position.TotalInvested);
        var totalInvestedAllTime = rows.Sum(x => x.Position.TotalInvested);
        var potentialPnlActive = activeRows.Sum(x => CalculatePotentialPnl(x.Position));
        var realizedPnlAllTime = closedRows.Sum(x => CalculateRealizedPnl(x.Position, x.Market));

        var closedByMarket = closedRows
            .Where(x => x.Market is not null)
            .GroupBy(x => x.Position.MarketId)
            .ToList();

        var closedMarkets = closedByMarket.Count;
        var wins = closedByMarket.Count(group =>
        {
            var outcome = ResolveOutcome(group.First().Market);
            if (outcome is null)
            {
                return false;
            }

            return group.Any(x =>
                x.Position.Contracts > 0
                && string.Equals(NormalizeSide(x.Position.Side), outcome, StringComparison.Ordinal)
            );
        });

        var accuracy = closedMarkets == 0
            ? 0m
            : Math.Round((decimal)wins / closedMarkets * 100m, 2, MidpointRounding.AwayFromZero);

        var normalizedScope = NormalizeScope(scope);
        var selectedTotal = normalizedScope == "all-time" ? totalInvestedAllTime : totalInvestedActive;

        var response = new PortfolioSummaryResponse
        {
            Scope = normalizedScope,
            ActivePositions = activeRows.Count,
            TotalInvested = selectedTotal,
            TotalInvestedActive = totalInvestedActive,
            TotalInvestedAllTime = totalInvestedAllTime,
            RealizedPnlAllTime = realizedPnlAllTime,
            PotentialPnlActive = potentialPnlActive,
            ClosedMarkets = closedMarkets,
            Wins = wins,
            AccuracyRate = accuracy,
        };

        try
        {
            await _portfolioRepository.AddPortfolioSnapshotAsync(
                new PortfolioSnapshot
                {
                    UserId = userId,
                    AsOf = DateTimeOffset.UtcNow,
                    ActivePositions = response.ActivePositions,
                    TotalInvestedActive = response.TotalInvestedActive,
                    TotalInvestedAllTime = response.TotalInvestedAllTime,
                    RealizedPnlAllTime = response.RealizedPnlAllTime,
                    PotentialPnlActive = response.PotentialPnlActive,
                    ClosedMarkets = response.ClosedMarkets,
                    Wins = response.Wins,
                    AccuracyRate = response.AccuracyRate,
                },
                ct
            );
        }
        catch
        {
            // snapshot is best-effort and should never fail summary rendering
        }

        return ServiceResult<PortfolioSummaryResponse>.Ok(response);
    }

    public async Task<ServiceResult<PortfolioPositionsResponse>> GetPositionsAsync(
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
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);
        var normalizedStatus = NormalizeStatus(status);

        var (positions, total) = await _portfolioRepository.GetUserPositionsPageAsync(
            userId,
            normalizedStatus,
            side,
            search,
            category,
            safePage,
            safePageSize,
            ct
        );

        var markets = await _portfolioRepository.GetMarketsByIdsMapAsync(
            positions.Select(x => x.MarketId),
            ct
        );

        var items = positions
            .Select(p =>
            {
                markets.TryGetValue(p.MarketId, out var market);
                var isActive = IsActive(p, market);
                return new PortfolioPositionItem
                {
                    PositionId = p.Id,
                    MarketId = p.MarketId,
                    MarketTitle = market?.Title ?? string.Empty,
                    MarketStatus = market?.Status ?? "unknown",
                    Side = NormalizeSide(p.Side).ToUpperInvariant(),
                    Contracts = p.Contracts,
                    AveragePrice = p.AveragePrice,
                    TotalInvested = p.TotalInvested,
                    PotentialPnl = isActive ? CalculatePotentialPnl(p) : 0m,
                    RealizedPnl = isActive ? 0m : CalculateRealizedPnl(p, market),
                    IsActive = isActive,
                    UpdatedAt = p.UpdatedAt,
                };
            })
            .ToList();

        return ServiceResult<PortfolioPositionsResponse>.Ok(
            new PortfolioPositionsResponse
            {
                Items = items,
                Total = total,
                Page = safePage,
                PageSize = safePageSize,
            }
        );
    }

    public async Task<ServiceResult<PortfolioFillsResponse>> GetFillsAsync(
        Guid userId,
        string? category,
        Guid? marketId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 200);

        var (fills, total) = await _portfolioRepository.GetUserPositionFillsPageAsync(
            userId,
            category,
            marketId,
            safePage,
            safePageSize,
            ct
        );

        var markets = await _portfolioRepository.GetMarketsByIdsMapAsync(
            fills.Select(x => x.MarketId),
            ct
        );

        var items = fills
            .Select(x =>
            {
                markets.TryGetValue(x.MarketId, out var market);
                return new PortfolioFillItem
                {
                    Id = x.Id,
                    PositionId = x.PositionId,
                    MarketId = x.MarketId,
                    MarketTitle = market?.Title ?? string.Empty,
                    Side = NormalizeSide(x.Side).ToUpperInvariant(),
                    Type = x.Type.ToUpperInvariant(),
                    Contracts = x.Contracts,
                    Price = x.Price,
                    GrossAmount = x.GrossAmount,
                    FeeAmount = x.FeeAmount,
                    NetAmount = x.NetAmount,
                    Source = x.Source.ToUpperInvariant(),
                    OrderId = x.OrderId,
                    IdempotencyKey = x.IdempotencyKey,
                    CreatedAt = x.CreatedAt,
                };
            })
            .ToList();

        return ServiceResult<PortfolioFillsResponse>.Ok(
            new PortfolioFillsResponse
            {
                Items = items,
                Total = total,
                Page = safePage,
                PageSize = safePageSize,
            }
        );
    }

    private static string NormalizeScope(string? scope)
    {
        var normalized = scope?.Trim().ToLowerInvariant();
        return normalized is "all" or "all-time" or "all_time" ? "all-time" : "active";
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return normalized is "active" or "closed" or "all" ? normalized : "active";
    }

    private static bool IsActive(Position position, Market? market)
    {
        var isPositionOpen =
            position.Contracts > 0
            && string.Equals(position.Status, "open", StringComparison.OrdinalIgnoreCase);
        var isMarketOpen =
            market is null || string.Equals(market.Status, "open", StringComparison.OrdinalIgnoreCase);
        return isPositionOpen && isMarketOpen;
    }

    private static decimal CalculatePotentialPnl(Position position)
    {
        var maxReturn = position.Contracts * 1.00m;
        var potential = maxReturn - position.TotalInvested;
        return Math.Round(potential, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateRealizedPnl(Position position, Market? market)
    {
        var outcome = ResolveOutcome(market);
        if (outcome is null)
        {
            return 0m;
        }

        var payout = string.Equals(NormalizeSide(position.Side), outcome, StringComparison.Ordinal)
            ? position.Contracts * 1.00m
            : 0m;

        var pnl = payout - position.TotalInvested;
        return Math.Round(pnl, 2, MidpointRounding.AwayFromZero);
    }

    private static string? ResolveOutcome(Market? market)
    {
        if (market is null)
        {
            return null;
        }

        var candidates = new[] { market.Status, market.ResolutionSource };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var value = candidate.Trim().ToLowerInvariant();
            if (
                value is "yes"
                    or "sim"
                    or "resolved_yes"
                    or "settled_yes"
                    or "closed_yes"
                    or "outcome_yes"
                    or "resultado_sim"
            )
            {
                return "yes";
            }

            if (
                value is "no"
                    or "nao"
                    or "não"
                    or "resolved_no"
                    or "settled_no"
                    or "closed_no"
                    or "outcome_no"
                    or "resultado_nao"
            )
            {
                return "no";
            }
        }

        return null;
    }

    private static string NormalizeSide(string? side)
    {
        if (string.IsNullOrWhiteSpace(side))
        {
            return "no";
        }

        var normalized = side.Trim().ToLowerInvariant();
        return normalized is "yes" or "sim" ? "yes" : "no";
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private sealed record PortfolioPositionRow(Position Position, Market? Market);
}
