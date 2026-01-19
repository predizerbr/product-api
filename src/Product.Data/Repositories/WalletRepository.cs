using Microsoft.EntityFrameworkCore;
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
            var stubUser = new User
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

    public async Task AddUserAsync(User user, CancellationToken ct = default)
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
