using Microsoft.EntityFrameworkCore;
using Pruduct.Business.Interfaces.Results;
using Pruduct.Business.Interfaces.Wallet;
using Pruduct.Common.Enums;
using Pruduct.Contracts.Wallet;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models.Wallet;

namespace Pruduct.Business.Services.Wallet;

public class WalletService : IWalletService
{
    private const string DefaultCurrency = "BRL";
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    private readonly AppDbContext _db;

    public WalletService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<bool>> ConfirmDepositAsync(
        Guid paymentIntentId,
        string providerPaymentId,
        CancellationToken ct = default
    )
    {
        var intent = await _db.PaymentIntents.FindAsync(new object?[] { paymentIntentId }, ct);
        if (intent is null)
            return ServiceResult<bool>.Fail("payment_intent_not_found");

        if (intent.Status == PaymentIntentStatus.APPROVED)
        {
            return ServiceResult<bool>.Ok(true); // idempotent
        }

        // marca como aprovado e cria ledger entry
        var accounts = await EnsureAccountsAsync(intent.UserId, ct);
        var account = accounts[0];

        _db.LedgerEntries.Add(
            new LedgerEntry
            {
                AccountId = account.Id,
                Type = LedgerEntryType.DEPOSIT_GATEWAY,
                Amount = intent.Amount,
                ReferenceType = "PaymentIntent",
                ReferenceId = intent.Id,
                IdempotencyKey = providerPaymentId ?? intent.IdempotencyKey,
            }
        );

        intent.Status = PaymentIntentStatus.APPROVED;
        intent.ExternalPaymentId ??= providerPaymentId;

        await _db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<IReadOnlyCollection<WalletBalanceResponse>>> GetBalancesAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var accounts = await EnsureAccountsAsync(userId, ct);
        var accountIds = accounts.Select(a => a.Id).ToArray();

        var balances = await _db
            .LedgerEntries.Where(le => accountIds.Contains(le.AccountId))
            .GroupBy(le => le.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(x => x.Amount) })
            .ToListAsync(ct);

        var balanceLookup = balances.ToDictionary(x => x.AccountId, x => x.Balance);

        var result = accounts
            .Select(a =>
            {
                var balance = balanceLookup.GetValueOrDefault(a.Id);
                return new WalletBalanceResponse
                {
                    Currency = a.Currency,
                    Balance = balance,
                    Available = balance,
                };
            })
            .ToList();

        return ServiceResult<IReadOnlyCollection<WalletBalanceResponse>>.Ok(result);
    }

    public async Task<ServiceResult<LedgerListResponse>> GetLedgerAsync(
        Guid userId,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    )
    {
        var accounts = await EnsureAccountsAsync(userId, ct);
        var accountIds = accounts.Select(a => a.Id).ToArray();
        var currencyByAccount = accounts.ToDictionary(a => a.Id, a => a.Currency);

        var pageSize = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        DateTimeOffset? cursorTime = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!DateTimeOffset.TryParse(cursor, out var parsed))
            {
                return ServiceResult<LedgerListResponse>.Fail("invalid_cursor");
            }

            cursorTime = parsed;
        }

        var query = _db.LedgerEntries.Where(le => accountIds.Contains(le.AccountId));

        if (cursorTime.HasValue)
        {
            query = query.Where(le => le.CreatedAt < cursorTime.Value);
        }

        var entries = await query
            .OrderByDescending(le => le.CreatedAt)
            .ThenByDescending(le => le.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = entries.Count > pageSize;
        var page = entries.Take(pageSize).ToList();
        var nextCursor = hasMore && page.Count > 0 ? page.Last().CreatedAt.ToString("o") : null;

        var response = page.Select(entry => new LedgerEntryResponse
            {
                Id = entry.Id,
                Type = entry.Type.ToString(),
                Amount = entry.Amount,
                Currency = currencyByAccount.GetValueOrDefault(entry.AccountId, DefaultCurrency),
                ReferenceType = entry.ReferenceType,
                ReferenceId = entry.ReferenceId,
                IdempotencyKey = entry.IdempotencyKey,
                CreatedAt = entry.CreatedAt,
            })
            .ToList();

        return ServiceResult<LedgerListResponse>.Ok(
            new LedgerListResponse { Entries = response, NextCursor = nextCursor }
        );
    }

    public async Task<ServiceResult<CreateDepositResponse>> CreateDepositIntentAsync(
        Guid userId,
        AmountRequest request,
        string idempotencyKey,
        CancellationToken ct = default
    )
    {
        var accounts = await EnsureAccountsAsync(userId, ct);
        var account = accounts[0];

        var existing = await _db.PaymentIntents.FirstOrDefaultAsync(
            x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
            ct
        );
        if (existing is not null)
        {
            return ServiceResult<CreateDepositResponse>.Ok(MapDeposit(existing));
        }

        var intent = new PaymentIntent
        {
            UserId = userId,
            Provider = "MANUAL",
            Amount = request.Amount,
            Currency = account.Currency,
            Status = PaymentIntentStatus.PENDING,
            IdempotencyKey = idempotencyKey,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
        };

        _db.PaymentIntents.Add(intent);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<CreateDepositResponse>.Ok(MapDeposit(intent));
    }

    public async Task<ServiceResult<DepositListResponse>> GetDepositsAsync(
        Guid userId,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    )
    {
        var pageSize = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        if (!TryParseCursor(cursor, out var cursorTime))
        {
            return ServiceResult<DepositListResponse>.Fail("invalid_cursor");
        }

        var query = _db.PaymentIntents.Where(x => x.UserId == userId);
        if (cursorTime.HasValue)
        {
            query = query.Where(x => x.CreatedAt < cursorTime.Value);
        }

        var intents = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = intents.Count > pageSize;
        var page = intents.Take(pageSize).ToList();
        var nextCursor = hasMore && page.Count > 0 ? page.Last().CreatedAt.ToString("o") : null;

        var items = page.Select(x => new DepositListItem
            {
                PaymentIntentId = x.Id,
                Provider = x.Provider,
                Status = x.Status.ToString(),
                Amount = x.Amount,
                Currency = x.Currency,
                CreatedAt = x.CreatedAt,
            })
            .ToList();

        return ServiceResult<DepositListResponse>.Ok(
            new DepositListResponse { Items = items, NextCursor = nextCursor }
        );
    }

    public async Task<ServiceResult<WithdrawalResponse>> CreateWithdrawalAsync(
        Guid userId,
        AmountRequest request,
        string idempotencyKey,
        CancellationToken ct = default
    )
    {
        var accounts = await EnsureAccountsAsync(userId, ct);
        var account = accounts[0];

        var existing = await _db.Withdrawals.FirstOrDefaultAsync(
            x => x.UserId == userId && x.IdempotencyKey == idempotencyKey,
            ct
        );
        if (existing is not null)
        {
            return ServiceResult<WithdrawalResponse>.Ok(MapWithdrawal(existing));
        }

        var balance = await _db
            .LedgerEntries.Where(le => le.AccountId == account.Id)
            .SumAsync(le => le.Amount, ct);

        if (balance < request.Amount)
        {
            return ServiceResult<WithdrawalResponse>.Fail("insufficient_funds");
        }

        var withdrawal = new Withdrawal
        {
            UserId = userId,
            Amount = request.Amount,
            Currency = account.Currency,
            Status = WithdrawalStatus.REQUESTED,
            IdempotencyKey = idempotencyKey,
        };

        _db.Withdrawals.Add(withdrawal);
        await _db.SaveChangesAsync(ct);

        _db.LedgerEntries.Add(
            new LedgerEntry
            {
                AccountId = account.Id,
                Type = LedgerEntryType.WITHDRAW_REQUEST,
                Amount = -request.Amount,
                ReferenceType = "Withdrawal",
                ReferenceId = withdrawal.Id,
                IdempotencyKey = idempotencyKey,
            }
        );

        await _db.SaveChangesAsync(ct);

        return ServiceResult<WithdrawalResponse>.Ok(MapWithdrawal(withdrawal));
    }

    public async Task<ServiceResult<WithdrawalListResponse>> GetWithdrawalsAsync(
        Guid userId,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    )
    {
        var pageSize = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        if (!TryParseCursor(cursor, out var cursorTime))
        {
            return ServiceResult<WithdrawalListResponse>.Fail("invalid_cursor");
        }

        var query = _db.Withdrawals.Where(x => x.UserId == userId);
        if (cursorTime.HasValue)
        {
            query = query.Where(x => x.CreatedAt < cursorTime.Value);
        }

        var withdrawals = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(pageSize + 1)
            .ToListAsync(ct);

        var hasMore = withdrawals.Count > pageSize;
        var page = withdrawals.Take(pageSize).ToList();
        var nextCursor = hasMore && page.Count > 0 ? page.Last().CreatedAt.ToString("o") : null;

        var items = page.Select(MapWithdrawalListItem).ToList();

        return ServiceResult<WithdrawalListResponse>.Ok(
            new WithdrawalListResponse { Items = items, NextCursor = nextCursor }
        );
    }

    public async Task<ServiceResult<WithdrawalResponse>> ApproveWithdrawalAsync(
        Guid withdrawalId,
        Guid adminUserId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    )
    {
        var withdrawal = await _db.Withdrawals.FirstOrDefaultAsync(x => x.Id == withdrawalId, ct);
        if (withdrawal is null)
        {
            return ServiceResult<WithdrawalResponse>.Fail("withdrawal_not_found");
        }

        if (withdrawal.Status != WithdrawalStatus.REQUESTED)
        {
            return ServiceResult<WithdrawalResponse>.Fail("invalid_status");
        }

        withdrawal.Status = WithdrawalStatus.APPROVED;
        withdrawal.ApprovedAt = DateTimeOffset.UtcNow;
        withdrawal.ApprovedByUserId = adminUserId;
        withdrawal.Notes = request.Notes;

        await _db.SaveChangesAsync(ct);

        return ServiceResult<WithdrawalResponse>.Ok(MapWithdrawal(withdrawal));
    }

    public async Task<ServiceResult<WithdrawalResponse>> RejectWithdrawalAsync(
        Guid withdrawalId,
        Guid adminUserId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    )
    {
        var withdrawal = await _db.Withdrawals.FirstOrDefaultAsync(x => x.Id == withdrawalId, ct);
        if (withdrawal is null)
        {
            return ServiceResult<WithdrawalResponse>.Fail("withdrawal_not_found");
        }

        if (withdrawal.Status != WithdrawalStatus.REQUESTED)
        {
            return ServiceResult<WithdrawalResponse>.Fail("invalid_status");
        }

        withdrawal.Status = WithdrawalStatus.REJECTED;
        withdrawal.ApprovedAt = DateTimeOffset.UtcNow;
        withdrawal.ApprovedByUserId = adminUserId;
        withdrawal.Notes = request.Notes;

        var account = await _db.Accounts.FirstOrDefaultAsync(
            x => x.UserId == withdrawal.UserId && x.Currency == withdrawal.Currency,
            ct
        );
        if (account is null)
        {
            return ServiceResult<WithdrawalResponse>.Fail("account_not_found");
        }

        _db.LedgerEntries.Add(
            new LedgerEntry
            {
                AccountId = account.Id,
                Type = LedgerEntryType.WITHDRAW_REQUEST,
                Amount = withdrawal.Amount,
                ReferenceType = "Withdrawal",
                ReferenceId = withdrawal.Id,
                IdempotencyKey = $"withdraw-reject-{withdrawal.Id}",
            }
        );

        await _db.SaveChangesAsync(ct);

        return ServiceResult<WithdrawalResponse>.Ok(MapWithdrawal(withdrawal));
    }

    private async Task<List<Account>> EnsureAccountsAsync(Guid userId, CancellationToken ct)
    {
        var accounts = await _db.Accounts.Where(a => a.UserId == userId).ToListAsync(ct);
        if (accounts.Count > 0)
        {
            return accounts;
        }

        var account = new Account { UserId = userId, Currency = DefaultCurrency };
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);

        return new List<Account> { account };
    }

    private static bool TryParseCursor(string? cursor, out DateTimeOffset? cursorTime)
    {
        cursorTime = null;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(cursor, out var parsed))
        {
            return false;
        }

        cursorTime = parsed;
        return true;
    }

    private static CreateDepositResponse MapDeposit(PaymentIntent intent) =>
        new()
        {
            PaymentIntentId = intent.Id,
            Provider = intent.Provider,
            Status = intent.Status.ToString(),
            Amount = intent.Amount,
            Currency = intent.Currency,
            CreatedAt = intent.CreatedAt,
            ExpiresAt = intent.ExpiresAt,
        };

    private static WithdrawalResponse MapWithdrawal(Withdrawal withdrawal) =>
        new()
        {
            Id = withdrawal.Id,
            Status = withdrawal.Status.ToString(),
            Amount = withdrawal.Amount,
            Currency = withdrawal.Currency,
            RequestedAt = withdrawal.CreatedAt,
        };

    private static WithdrawalListItem MapWithdrawalListItem(Withdrawal withdrawal) =>
        new()
        {
            Id = withdrawal.Id,
            Status = withdrawal.Status.ToString(),
            Amount = withdrawal.Amount,
            Currency = withdrawal.Currency,
            RequestedAt = withdrawal.CreatedAt,
        };
}
