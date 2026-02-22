using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Product.Business.Interfaces.Results;
using Product.Contracts.Wallet;

namespace Product.Business.Interfaces.Wallet;

public interface IWalletService
{
    Task<ApiResult> GetBalancesApiAsync(ClaimsPrincipal principal, CancellationToken ct = default);
    Task<ApiResult> GetSummaryApiAsync(ClaimsPrincipal principal, CancellationToken ct = default);

    Task<ApiResult> GetLedgerApiAsync(
        ClaimsPrincipal principal,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    );

    Task<ApiResult> CreateDepositIntentApiAsync(
        ClaimsPrincipal principal,
        IHeaderDictionary headers,
        AmountRequest request,
        CancellationToken ct = default
    );

    Task<ApiResult> GetDepositsApiAsync(
        ClaimsPrincipal principal,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    );

    Task<ApiResult> CreateWithdrawalApiAsync(
        ClaimsPrincipal principal,
        IHeaderDictionary headers,
        AmountRequest request,
        CancellationToken ct = default
    );

    Task<ApiResult> GetWithdrawalsApiAsync(
        ClaimsPrincipal principal,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    );

    Task<ApiResult> ApproveWithdrawalApiAsync(
        ClaimsPrincipal principal,
        Guid withdrawalId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    );

    Task<ApiResult> RejectWithdrawalApiAsync(
        ClaimsPrincipal principal,
        Guid withdrawalId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    );

    Task<ServiceResult<IReadOnlyCollection<WalletBalanceResponse>>> GetBalancesAsync(
        Guid userId,
        CancellationToken ct = default
    );

    Task<ServiceResult<WalletSummaryResponse>> GetSummaryAsync(
        Guid userId,
        CancellationToken ct = default
    );

    Task<ServiceResult<LedgerListResponse>> GetLedgerAsync(
        Guid userId,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    );

    Task<ServiceResult<CreateDepositResponse>> CreateDepositIntentAsync(
        Guid userId,
        AmountRequest request,
        string idempotencyKey,
        CancellationToken ct = default
    );

    Task<ServiceResult<DepositListResponse>> GetDepositsAsync(
        Guid userId,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    );

    Task<ServiceResult<WithdrawalResponse>> CreateWithdrawalAsync(
        Guid userId,
        AmountRequest request,
        string idempotencyKey,
        CancellationToken ct = default
    );

    Task<ServiceResult<WithdrawalListResponse>> GetWithdrawalsAsync(
        Guid userId,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    );

    Task<ServiceResult<WithdrawalResponse>> ApproveWithdrawalAsync(
        Guid withdrawalId,
        Guid adminUserId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    );

    Task<ServiceResult<WithdrawalResponse>> RejectWithdrawalAsync(
        Guid withdrawalId,
        Guid adminUserId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    );

    Task<ServiceResult<bool>> ConfirmDepositAsync(
        Guid paymentIntentId,
        string providerPaymentId,
        CancellationToken ct = default
    );

    Task<ServiceResult<bool>> SyncDepositStatusAsync(
        Guid paymentIntentId,
        string? providerStatus,
        string? providerStatusDetail,
        string? providerPaymentId,
        decimal? providerAmount,
        CancellationToken ct = default
    );

}
