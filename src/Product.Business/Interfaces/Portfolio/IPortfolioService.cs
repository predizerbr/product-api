using System.Security.Claims;
using Product.Business.Interfaces.Results;
using Product.Contracts.Portfolio;

namespace Product.Business.Interfaces.Portfolio;

public interface IPortfolioService
{
    Task<ApiResult> GetSummaryApiAsync(
        ClaimsPrincipal principal,
        string? scope,
        CancellationToken ct = default
    );

    Task<ApiResult> GetPositionsApiAsync(
        ClaimsPrincipal principal,
        string? status,
        string? side,
        string? search,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task<ApiResult> GetFillsApiAsync(
        ClaimsPrincipal principal,
        string? category,
        Guid? marketId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task<ServiceResult<PortfolioSummaryResponse>> GetSummaryAsync(
        Guid userId,
        string? scope,
        CancellationToken ct = default
    );

    Task<ServiceResult<PortfolioPositionsResponse>> GetPositionsAsync(
        Guid userId,
        string? status,
        string? side,
        string? search,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task<ServiceResult<PortfolioFillsResponse>> GetFillsAsync(
        Guid userId,
        string? category,
        Guid? marketId,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
}
