using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pruduct.Api.Contracts;
using Pruduct.Business.Interfaces.Wallet;
using Pruduct.Contracts.Wallet;

namespace Pruduct.Api.Controllers;

[ApiController]
[Route("api/v1/wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;

    public WalletController(IWalletService walletService)
    {
        _walletService = walletService;
    }

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _walletService.GetBalancesAsync(userId, ct);
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        return Ok(new ResponseEnvelope<IReadOnlyCollection<WalletBalanceResponse>>(result.Data!));
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedger(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _walletService.GetLedgerAsync(userId, cursor, limit, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "invalid_cursor"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<LedgerListResponse>(result.Data!));
    }

    [HttpPost("deposits/intent")]
    public async Task<IActionResult> CreateDepositIntent(
        [FromBody] AmountRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        if (!TryGetIdempotencyKey(out var idempotencyKey))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "idempotency_required"
            );
        }

        var result = await _walletService.CreateDepositIntentAsync(
            userId,
            request,
            idempotencyKey,
            ct
        );
        if (!result.Success)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        return Ok(new ResponseEnvelope<CreateDepositResponse>(result.Data!));
    }

    [HttpGet("deposits")]
    public async Task<IActionResult> GetDeposits(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _walletService.GetDepositsAsync(userId, cursor, limit, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "invalid_cursor"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<DepositListResponse>(result.Data!));
    }

    [HttpPost("withdrawals")]
    public async Task<IActionResult> CreateWithdrawal(
        [FromBody] AmountRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        if (!TryGetIdempotencyKey(out var idempotencyKey))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "idempotency_required"
            );
        }

        var result = await _walletService.CreateWithdrawalAsync(
            userId,
            request,
            idempotencyKey,
            ct
        );
        if (!result.Success)
        {
            var status =
                result.Error == "insufficient_funds"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status400BadRequest;
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<WithdrawalResponse>(result.Data!));
    }

    [HttpGet("withdrawals")]
    public async Task<IActionResult> GetWithdrawals(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var userId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _walletService.GetWithdrawalsAsync(userId, cursor, limit, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "invalid_cursor"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<WithdrawalListResponse>(result.Data!));
    }

    [HttpPost("withdrawals/{withdrawalId:guid}/approve")]
    [Authorize(Policy = "RequireAdminL2")]
    public async Task<IActionResult> ApproveWithdrawal(
        Guid withdrawalId,
        [FromBody] WithdrawalDecisionRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _walletService.ApproveWithdrawalAsync(
            withdrawalId,
            adminUserId,
            request,
            ct
        );
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "withdrawal_not_found" => StatusCodes.Status404NotFound,
                "invalid_status" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<WithdrawalResponse>(result.Data!));
    }

    [HttpPost("withdrawals/{withdrawalId:guid}/reject")]
    [Authorize(Policy = "RequireAdminL2")]
    public async Task<IActionResult> RejectWithdrawal(
        Guid withdrawalId,
        [FromBody] WithdrawalDecisionRequest request,
        CancellationToken ct
    )
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "invalid_token");
        }

        var result = await _walletService.RejectWithdrawalAsync(
            withdrawalId,
            adminUserId,
            request,
            ct
        );
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "withdrawal_not_found" => StatusCodes.Status404NotFound,
                "invalid_status" => StatusCodes.Status400BadRequest,
                "account_not_found" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(statusCode: status, title: result.Error);
        }

        return Ok(new ResponseEnvelope<WithdrawalResponse>(result.Data!));
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private bool TryGetIdempotencyKey(out string idempotencyKey)
    {
        idempotencyKey = string.Empty;
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values))
        {
            return false;
        }

        var value = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        idempotencyKey = value;
        return true;
    }
}
