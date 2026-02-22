using Product.Data.Models.Users;
using Product.Data.Models.Wallet;

namespace Product.Data.Interfaces.Repositories;

public interface IWalletRepository
{
    Task<List<Account>> EnsureAccountsAsync(
        Guid userId,
        string defaultCurrency,
        CancellationToken ct = default
    );
    Task<Account?> GetAccountByUserAndCurrencyAsync(
        Guid userId,
        string currency,
        CancellationToken ct = default
    );
    Task<PaymentIntent?> GetPaymentIntentByIdAsync(
        Guid paymentIntentId,
        CancellationToken ct = default
    );
    Task<PaymentIntent?> GetPaymentIntentByIdempotencyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default
    );
    Task AddPaymentIntentAsync(PaymentIntent intent, CancellationToken ct = default);
    Task<List<PaymentIntent>> GetPaymentIntentsAsync(
        Guid userId,
        DateTimeOffset? cursorTime,
        int take,
        CancellationToken ct = default
    );
    Task<List<PaymentIntent>> GetPaymentIntentsByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    );
    Task<Withdrawal?> GetWithdrawalByIdAsync(Guid withdrawalId, CancellationToken ct = default);
    Task<Withdrawal?> GetWithdrawalByIdempotencyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default
    );
    Task AddWithdrawalAsync(Withdrawal withdrawal, CancellationToken ct = default);
    Task<List<Withdrawal>> GetWithdrawalsAsync(
        Guid userId,
        DateTimeOffset? cursorTime,
        int take,
        CancellationToken ct = default
    );
    Task<LedgerEntry?> GetLedgerEntryByReferenceAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken ct = default
    );
    Task AddLedgerEntryAsync(LedgerEntry entry, CancellationToken ct = default);
    Task AddLedgerEntriesAsync(IEnumerable<LedgerEntry> entries, CancellationToken ct = default);
    Task UpdatePaymentIntentAsync(PaymentIntent intent, CancellationToken ct = default);
    Task UpdateWithdrawalAsync(Withdrawal withdrawal, CancellationToken ct = default);
    Task<decimal> GetAccountBalanceAsync(Guid accountId, CancellationToken ct = default);
    Task<Dictionary<Guid, decimal>> GetLedgerBalancesAsync(
        Guid[] accountIds,
        CancellationToken ct = default
    );
    Task<decimal> GetTotalDepositedAsync(
        Guid userId,
        string currency,
        CancellationToken ct = default
    );
    Task<decimal> GetTotalWithdrawnAsync(
        Guid userId,
        string currency,
        CancellationToken ct = default
    );
    Task<decimal> GetTotalBoughtAsync(Guid userId, CancellationToken ct = default);
    Task<List<LedgerEntry>> GetLedgerEntriesAsync(
        Guid[] accountIds,
        DateTimeOffset? cursorTime,
        int take,
        CancellationToken ct = default
    );
    Task<List<string>> GetLedgerEntryIdempotencyKeysAsync(
        Guid accountId,
        CancellationToken ct = default
    );
    Task<LedgerEntry?> GetLedgerEntryByIdAsync(Guid id, CancellationToken ct = default);
    Task AddReceiptAsync(Receipt receipt, CancellationToken ct = default);
    Task<List<Receipt>> GetReceiptsAsync(
        Guid userId,
        DateTimeOffset? cursorTime,
        int take,
        CancellationToken ct = default
    );
    Task<Receipt?> GetReceiptByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Product.Data.Models.Markets.Market>> GetMarketsByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    );
    Task<List<PaymentIntent>> GetApprovedPaymentIntentsWithoutReceiptAsync(
        int take,
        CancellationToken ct = default
    );
    Task<bool> ReceiptExistsForReferenceAsync(Guid referenceId, CancellationToken ct = default);
    Task<List<Product.Data.Models.Markets.Transaction>> GetBuyTransactionsWithoutReceiptAsync(
        int take,
        CancellationToken ct = default
    );
    Task<LedgerEntry?> FindLedgerEntryForBuyAsync(
        Guid userId,
        Guid marketId,
        decimal amount,
        DateTimeOffset txCreatedAt,
        CancellationToken ct = default
    );
    Task AddUserAsync(ApplicationUser user, CancellationToken ct = default);
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
