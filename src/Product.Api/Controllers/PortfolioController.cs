using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Api.Extensions;
using Product.Business.Interfaces.Portfolio;

namespace Product.Api.Controllers;

[ApiController]
[Route("api/v1/portfolio")]
[Authorize]
public class PortfolioController(IPortfolioService portfolioService) : ControllerBase
{
    private readonly IPortfolioService _portfolioService = portfolioService;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] string? scope, CancellationToken ct)
    {
        var result = await _portfolioService.GetSummaryApiAsync(User, scope, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("positions")]
    public async Task<IActionResult> GetPositions(
        [FromQuery] string? status,
        [FromQuery] string? side,
        [FromQuery] string? search,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var result = await _portfolioService.GetPositionsApiAsync(
            User,
            status,
            side,
            search,
            category,
            page,
            pageSize,
            ct
        );
        return this.ToActionResult(result);
    }

    [HttpGet("fills")]
    public async Task<IActionResult> GetFills(
        [FromQuery] string? category,
        [FromQuery] Guid? marketId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var result = await _portfolioService.GetFillsApiAsync(
            User,
            category,
            marketId,
            page,
            pageSize,
            ct
        );
        return this.ToActionResult(result);
    }
}
