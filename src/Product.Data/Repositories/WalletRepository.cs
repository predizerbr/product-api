using Microsoft.EntityFrameworkCore;
using Product.Common.Enums;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users;
using Product.Data.Models.Wallet;

namespace Product.Data.Repositories;

public class WalletRepository(AppDbContext db) : IWalletRepository
{
    public async Task<List<Account>> EnsureAccountsAsync(
        Guid userId,
        string defaultCurrency,
        CancellationToken ct = default
    )
    {
        var accounts = await db.Accounts.Where(a => a.UserId == userId).ToListAsync(ct);
        if (accounts.Count > 0)
        {
            return accounts;
        }

        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
        {
            var stubUser = new ApplicationUser
            {
                Id = userId,
                UserName = userId.ToString(),
                NormalizedUserName = userId.ToString().ToUpperInvariant(),
                Email = $"{userId}@local.invalid",
                NormalizedEmail = $"{userId}@local.invalid".ToUpperInvariant(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.Users.Add(stubUser);
            await db.SaveChangesAsync(ct);
        }

        var account = new Account { UserId = userId, Currency = defaultCurrency };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);

        return new List<Account> { account };
    }

    public async Task<Account?> GetAccountByUserAndCurrencyAsync(
        Guid userId,
        string currency,
        CancellationToken ct = default
    )
    {
        return await db.Accounts.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Currency == currency,
            ct
        );
    }

    public async Task<PaymentIntent?> GetPaymentIntentByIdAsync(
        Guid paymentIntentId,
        CancellationToken ct = default
    )
    {
        return await db.PaymentIntents.FindAsync(new object?[] { paymentIntentId }, ct);
    }

    public async Task<PaymentIntent?> GetPaymentIntentByIdempotencyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default
    )
    {
        return await db.PaymentIntents.FirstOrDefaultAsync(
            x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
            ct
        );
    }

    public async Task AddPaymentIntentAsync(PaymentIntent intent, CancellationToken ct = default)
    {
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PaymentIntent>> GetPaymentIntentsAsync(
        Guid userId,
        DateTimeOffset? cursorTime,
        int take,
        CancellationToken ct = default
    )
    {
        var query = db.PaymentIntents.Where(x => x.UserId == userId);
        if (cursorTime.HasValue)
        {
            query = query.Where(x => x.CreatedAt < cursorTime.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<PaymentIntent>> GetPaymentIntentsByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    )
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new List<PaymentIntent>();
        }

        return await db.PaymentIntents.Where(pi => idList.Contains(pi.Id)).ToListAsync(ct);
    }

    public async Task<Withdrawal?> GetWithdrawalByIdAsync(
        Guid withdrawalId,
        CancellationToken ct = default
    )
    {
        return await db.Withdrawals.FirstOrDefaultAsync(x => x.Id == withdrawalId, ct);
    }

    public async Task<Withdrawal?> GetWithdrawalByIdempotencyAsync(
        Guid userId,
        string idempotencyKey,
        CancellationToken ct = default
    )
    {
        return await db.Withdrawals.FirstOrDefaultAsync(
            x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
            ct
        );
    }

    public async Task AddWithdrawalAsync(Withdrawal withdrawal, CancellationToken ct = default)
    {
        db.Withdrawals.Add(withdrawal);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Withdrawal>> GetWithdrawalsAsync(
        Guid userId,
        DateTimeOffset? cursorTime,
        int take,
        CancellationToken ct = default
    )
    {
        var query = db.Withdrawals.Where(x => x.UserId == userId);
        if (cursorTime.HasValue)
        {
            query = query.Where(x => x.CreatedAt < cursorTime.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<LedgerEntry?> GetLedgerEntryByReferenceAsync(
        string referenceType,
        Guid referenceId,
        CancellationToken ct = default
    )
    {
        return await db.LedgerEntries.FirstOrDefaultAsync(
            le => le.ReferenceType == referenceType && le.ReferenceId == referenceId,
            ct
        );
    }

    public async Task AddLedgerEntryAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddLedgerEntriesAsync(
        IEnumerable<LedgerEntry> entries,
        CancellationToken ct = default
    )
    {
        db.LedgerEntries.AddRange(entries);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdatePaymentIntentAsync(PaymentIntent intent, CancellationToken ct = default)
    {
        db.PaymentIntents.Update(intent);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateWithdrawalAsync(Withdrawal withdrawal, CancellationToken ct = default)
    {
        db.Withdrawals.Update(withdrawal);
        await db.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetAccountBalanceAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        return await db
            .LedgerEntries.Where(le => le.AccountId == accountId)
            .SumAsync(le => le.Amount, ct);
    }

    public async Task<Dictionary<Guid, decimal>> GetLedgerBalancesAsync(
        Guid[] accountIds,
        CancellationToken ct = default
    )
    {
        var balances = await db
            .LedgerEntries.Where(le => accountIds.Contains(le.AccountId))
            .GroupBy(le => le.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        return balances.ToDictionary(x => x.AccountId, x => x.Balance);
    }

    public async Task<decimal> GetTotalDepositedAsync(
        Guid userId,
        string currency,
        CancellationToken ct = default
    )
    {
        return await db
                .PaymentIntents.Where(x =>
                    x.UserId == userId
                    && x.Currency == currency
                    && x.Status == PaymentIntentStatus.APPROVED
                )
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
    }

    public async Task<decimal> GetTotalWithdrawnAsync(
        Guid userId,
        string currency,
        CancellationToken ct = default
    )
    {
        return await db
                .Withdrawals.Where(x =>
                    x.UserId == userId
                    && x.Currency == currency
                    && (x.Status == WithdrawalStatus.APPROVED || x.Status == WithdrawalStatus.PAID)
                )
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
    }

    public async Task<decimal> GetTotalBoughtAsync(Guid userId, CancellationToken ct = default)
    {
        return await db
                .MarketTransactions.Where(x => x.UserId == userId && x.Type == "buy")
                .SumAsync(x => (decimal?)x.NetAmount, ct) ?? 0m;
    }

    public async Task<List<LedgerEntry>> GetLedgerEntriesAsync(
        Guid[] accountIds,
        DateTimeOffset? cursorTime,
        int take,
        CancellationToken ct = default
    )
    {
        var query = db.LedgerEntries.Where(le => accountIds.Contains(le.AccountId));
        if (cursorTime.HasValue)
        {
            query = query.Where(le => le.CreatedAt < cursorTime.Value);
        }

        return await query
            .OrderByDescending(le => le.CreatedAt)
            .ThenByDescending(le => le.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetLedgerEntryIdempotencyKeysAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        return await db
            .LedgerEntries.Where(le => le.AccountId == accountId && le.IdempotencyKey != null)
            .Select(le => le.IdempotencyKey!)
            .ToListAsync(ct);
    }

    public async Task<LedgerEntry?> GetLedgerEntryByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.LedgerEntries.FindAsync(new object?[] { id }, ct);
    }

    public async Task AddReceiptAsync(Receipt receipt, CancellationToken ct = default)
    {
        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Receipt>> GetReceiptsAsync(
        Guid userId,
        DateTimeOffset? cursorTime,
        int take,
        CancellationToken ct = default
    )
    {
        var query = db.Receipts.Where(r => r.UserId == userId);
        if (cursorTime.HasValue)
            query = query.Where(r => r.CreatedAt < cursorTime.Value);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<Receipt?> GetReceiptByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Receipts.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<Product.Data.Models.Markets.Market>> GetMarketsByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct = default
    )
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new List<Product.Data.Models.Markets.Market>();
        }

        return await db.Markets.Where(m => idList.Contains(m.Id)).ToListAsync(ct);
    }

    public async Task<List<PaymentIntent>> GetApprovedPaymentIntentsWithoutReceiptAsync(
        int take,
        CancellationToken ct = default
    )
    {
        // A intent está coberta se houver recibo apontando para ela diretamente
        // ou se houver recibo apontando para o ledger entry gerado a partir dela.
        var coveredIntentIds =
            from r in db.Receipts
            where r.ReferenceId != null
            join le in db.LedgerEntries on r.ReferenceId equals le.Id into leJoin
            from le in leJoin.DefaultIfEmpty()
            select new
            {
                DirectIntentId = r.ReferenceType == "PaymentIntent" ? r.ReferenceId : null,
                LedgerIntentId = le != null && le.ReferenceType == "PaymentIntent"
                    ? le.ReferenceId
                    : null,
            };

        var coveredIds = await coveredIntentIds
            .Select(x => x.DirectIntentId ?? x.LedgerIntentId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToListAsync(ct);

        return await db
            .PaymentIntents.Where(pi => pi.Status == Common.Enums.PaymentIntentStatus.APPROVED)
            .Where(pi => !coveredIds.Contains(pi.Id))
            .OrderBy(pi => pi.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<bool> ReceiptExistsForReferenceAsync(
        Guid referenceId,
        CancellationToken ct = default
    )
    {
        return await db.Receipts.AnyAsync(r => r.ReferenceId == referenceId, ct);
    }

    public async Task<
        List<Product.Data.Models.Markets.Transaction>
    > GetBuyTransactionsWithoutReceiptAsync(int take, CancellationToken ct = default)
    {
        // Coberta se houver recibo referenciando a própria transação ou o ledger gerado para ela.
        var coveredTxIds =
            from r in db.Receipts
            where r.ReferenceId != null
            join le in db.LedgerEntries on r.ReferenceId equals le.Id into leJoin
            from le in leJoin.DefaultIfEmpty()
            select new
            {
                DirectTxId = r.ReferenceType == "MarketTransaction" ? r.ReferenceId : null,
                LedgerTxId = le != null && le.ReferenceType == "market_trade"
                    ? le.ReferenceId
                    : null,
            };

        var coveredIds = await coveredTxIds
            .Select(x => x.DirectTxId ?? x.LedgerTxId)
            .Where(id => id != null)
            .Select(id => id!.Value)
            .Distinct()
            .ToListAsync(ct);

        return await db
            .MarketTransactions.Where(mt => mt.Type == "buy")
            .Where(mt => !coveredIds.Contains(mt.Id))
            .OrderBy(mt => mt.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<LedgerEntry?> FindLedgerEntryForBuyAsync(
        Guid userId,
        Guid marketId,
        decimal amount,
        DateTimeOffset txCreatedAt,
        CancellationToken ct = default
    )
    {
        var accountIds = await db
            .Accounts.Where(a => a.UserId == userId)
            .Select(a => a.Id)
            .ToListAsync(ct);
        if (accountIds.Count == 0)
            return null;

        var query = db
            .LedgerEntries.Where(le =>
                accountIds.Contains(le.AccountId)
                && le.ReferenceType == "market_trade"
                && le.ReferenceId == marketId
                && le.Amount == amount
            )
            .OrderByDescending(le => le.CreatedAt);

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task AddUserAsync(ApplicationUser user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Users.AnyAsync(u => u.Id == userId, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
