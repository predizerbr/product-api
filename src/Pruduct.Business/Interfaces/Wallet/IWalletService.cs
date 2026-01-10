using Pruduct.Business.Interfaces.Results;
using Pruduct.Contracts.Wallet;

namespace Pruduct.Business.Interfaces.Wallet;

public interface IWalletService
{
    Task<ServiceResult<IReadOnlyCollection<WalletBalanceResponse>>> GetBalancesAsync(
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
}
