using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Product.Api.Extensions;
using Product.Business.Interfaces.Wallet;
using Product.Contracts.Wallet;

namespace Product.Api.Controllers;

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
        var result = await _walletService.GetBalancesApiAsync(User, ct);
        return this.ToActionResult(result);
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedger(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct
    )
    {
        var result = await _walletService.GetLedgerApiAsync(User, cursor, limit, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("deposits/intent")]
    public async Task<IActionResult> CreateDepositIntent(
        [FromBody] AmountRequest request,
        CancellationToken ct
    )
    {
        var result = await _walletService.CreateDepositIntentApiAsync(
            User,
            Request.Headers,
            request,
            ct
        );
        return this.ToActionResult(result);
    }

    [HttpGet("deposits")]
    public async Task<IActionResult> GetDeposits(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct
    )
    {
        var result = await _walletService.GetDepositsApiAsync(User, cursor, limit, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("withdrawals")]
    public async Task<IActionResult> CreateWithdrawal(
        [FromBody] AmountRequest request,
        CancellationToken ct
    )
    {
        var result = await _walletService.CreateWithdrawalApiAsync(
            User,
            Request.Headers,
            request,
            ct
        );
        return this.ToActionResult(result);
    }

    [HttpGet("withdrawals")]
    public async Task<IActionResult> GetWithdrawals(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct
    )
    {
        var result = await _walletService.GetWithdrawalsApiAsync(User, cursor, limit, ct);
        return this.ToActionResult(result);
    }

    [HttpPost("withdrawals/{withdrawalId:guid}/approve")]
    [Authorize(Policy = "RequireAdminL2")]
    public async Task<IActionResult> ApproveWithdrawal(
        Guid withdrawalId,
        [FromBody] WithdrawalDecisionRequest request,
        CancellationToken ct
    )
    {
        var result = await _walletService.ApproveWithdrawalApiAsync(
            User,
            withdrawalId,
            request,
            ct
        );
        return this.ToActionResult(result);
    }

    [HttpPost("withdrawals/{withdrawalId:guid}/reject")]
    [Authorize(Policy = "RequireAdminL2")]
    public async Task<IActionResult> RejectWithdrawal(
        Guid withdrawalId,
        [FromBody] WithdrawalDecisionRequest request,
        CancellationToken ct
    )
    {
        var result = await _walletService.RejectWithdrawalApiAsync(User, withdrawalId, request, ct);
        return this.ToActionResult(result);
    }
}
