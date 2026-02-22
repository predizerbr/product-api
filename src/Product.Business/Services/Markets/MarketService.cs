using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Product.Business.Interfaces;
using Product.Business.Interfaces.Categories;
using Product.Business.Interfaces.Market;
using Product.Business.Interfaces.Notifications;
using Product.Common.Enums;
using Product.Contracts.Markets;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Markets;
using Product.Data.Models.Portfolio;
using Product.Data.Models.Wallet;

namespace Product.Business.Services.Markets;

public class MarketService : IMarketService
{
    private readonly IMarketRepository _marketRepo;
    private readonly AppDbContext _db;
    private readonly IMarketNotifier _notifier;
    private readonly IWalletRepository _walletRepo;
    private readonly ICategoryService _categoryService;
    private readonly ILogger<MarketService> _logger;

    public MarketService(
        IMarketRepository marketRepo,
        AppDbContext db,
        IMarketNotifier notifier,
        IWalletRepository walletRepo,
        ICategoryService categoryService,
        ILogger<MarketService> logger
    )
    {
        _marketRepo = marketRepo;
        _db = db;
        _notifier = notifier;
        _walletRepo = walletRepo;
        _categoryService = categoryService;
        _logger = logger;
    }

    public async Task<(
        IEnumerable<MarketResponse> Items,
        int Total,
        int Page,
        int PageSize
    )> ExploreMarketsAsync(
        Product.Contracts.Markets.ExploreFilterRequest req,
        CancellationToken ct = default
    )
    {
        var q = _db.Markets.AsQueryable();

        // only open markets (UI shows available markets)
        q = q.Where(m => m.Status == "open");

        // category filtering: support multiple categories via `Categories` array or legacy `Category` string
        var categoriesList = new List<string>();
        if (req.Categories != null && req.Categories.Length > 0)
        {
            foreach (var raw in req.Categories)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                // allow comma-separated in a single value
                var parts = raw.Split(',')
                    .Select(p => p.Trim().ToLowerInvariant())
                    .Where(p => !string.IsNullOrEmpty(p));
                categoriesList.AddRange(parts);
            }
        }
        else if (!string.IsNullOrWhiteSpace(req.Category))
        {
            categoriesList.AddRange(
                req.Category.Split(',')
                    .Select(p => p.Trim().ToLowerInvariant())
                    .Where(p => !string.IsNullOrEmpty(p))
            );
        }

        if (categoriesList.Count > 0)
        {
            // normalize unique
            categoriesList = categoriesList.Select(c => c.ToLowerInvariant()).Distinct().ToList();
            if (categoriesList.Contains("todas"))
            {
                // no filter
            }
            else
            {
                var includeNovidades = categoriesList.Any(c => c == "novidades" || c == "novidade");
                var includeEmAlta = categoriesList.Any(c => c == "em-alta" || c == "em alta");
                var slugList = categoriesList
                    .Except(new[] { "novidades", "novidade", "em-alta", "em alta" })
                    .Select(c => c.ToUpperInvariant())
                    .ToList();

                q = q.Where(m =>
                    (includeNovidades && m.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-7))
                    || (
                        includeEmAlta
                        && (
                            m.Volume24h > 0
                            || m.VolumeTotal > 1000m
                            || (m.YesContracts + m.NoContracts) > 100
                        )
                    )
                    || (
                        slugList.Count > 0
                        && (
                            (m.Category != null && slugList.Contains(m.Category.ToUpper()))
                            || _db.MarketCategories.Any(mc =>
                                mc.MarketId == m.Id
                                && mc.Category != null
                                && slugList.Contains(mc.Category.Slug!)
                            )
                        )
                    )
                );

                if (includeNovidades)
                {
                    q = q.OrderByDescending(m => m.CreatedAt);
                }
            }
        }

        // search
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            var like = $"{s}%";
            q = q.Where(m =>
                EF.Functions.ILike(m.Title, like)
                || (m.Description != null && EF.Functions.ILike(m.Description, like))
            );
        }

        // sorting
        if (!string.IsNullOrWhiteSpace(req.Sort))
        {
            switch (req.Sort.ToLowerInvariant())
            {
                case "recent":
                    q = q.OrderByDescending(m => m.CreatedAt);
                    break;
                case "probability":
                    q = q.OrderByDescending(m => m.YesPrice);
                    break;
                default:
                    q = q.OrderByDescending(m => m.VolumeTotal);
                    break;
            }
        }

        var page = Math.Max(req.Page, 1);
        var pageSize = Math.Clamp(req.PageSize, 1, 200);

        var total = await q.CountAsync(ct);

        var items = await q.Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MarketResponse
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Category = m.Category,
                ClosingDate = m.ClosingDate,
                ResolutionSource = m.ResolutionSource,
                Status = m.Status,
                Featured = m.Featured,
                YesPrice = m.YesPrice,
                NoPrice = m.NoPrice,
                VolumeTotal = m.VolumeTotal,
                YesContracts = m.YesContracts,
                NoContracts = m.NoContracts,
            })
            .ToListAsync(ct);

        return (items, total, page, pageSize);
    }

    public async Task<MarketResponse?> GetMarketAsync(
        Guid marketId,
        Guid? userId = null,
        CancellationToken ct = default
    )
    {
        var m = await _marketRepo.GetByIdAsync(marketId, ct);
        if (m == null)
            return null;
        return new MarketResponse
        {
            Id = m.Id,
            Title = m.Title,
            Description = m.Description,
            Category = m.Category,
            ClosingDate = m.ClosingDate,
            ResolutionSource = m.ResolutionSource,
            Status = m.Status,
            Featured = m.Featured,
            YesPrice = m.YesPrice,
            NoPrice = m.NoPrice,
            VolumeTotal = m.VolumeTotal,
            YesContracts = m.YesContracts,
            NoContracts = m.NoContracts,
        };
    }

    public async Task<IEnumerable<MarketHistoryPoint>> GetMarketHistoryAsync(
        Guid marketId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? resolution = null,
        CancellationToken ct = default
    )
    {
        // Simple implementation for now: return current snapshot as a single point.
        // Future: aggregate from `MarketTransactions` or a dedicated `MarketPrice` table.
        var market = await _marketRepo.GetByIdAsync(marketId, ct);
        if (market == null)
            return Enumerable.Empty<MarketHistoryPoint>();

        var point = new MarketHistoryPoint
        {
            Timestamp = DateTimeOffset.UtcNow,
            YesPrice = market.YesPrice,
            NoPrice = market.NoPrice,
            Volume = market.VolumeTotal,
        };

        return new[] { point };
    }

    public async Task<BuyResponse> BuyAsync(
        Guid marketId,
        Guid userId,
        string side,
        decimal amount,
        string? idempotencyKey = null,
        CancellationToken ct = default
    )
    {
        if (amount <= 0)
            throw new ArgumentException("invalid_amount");
        if (side != "yes" && side != "no")
            throw new ArgumentException("invalid_side");

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await _db.IdempotencyRecords.FirstOrDefaultAsync(
                i => i.Key == idempotencyKey && i.UserId == userId,
                ct
            );
            if (existing != null)
            {
                return JsonSerializer.Deserialize<BuyResponse>(existing.ResultPayload)!;
            }
        }

        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );
        try
        {
            var market = await _db.Markets.Where(x => x.Id == marketId).FirstOrDefaultAsync(ct);
            if (market == null)
                throw new InvalidOperationException("market_not_found");
            if (market.Status != "open")
                throw new InvalidOperationException("market_not_open");
            if (market.ClosingDate.HasValue && market.ClosingDate <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("market_closing_or_closed");

            var price = side == "yes" ? market.YesPrice : market.NoPrice;
            var contracts = (int)Math.Floor(amount / price);
            if (contracts <= 0)
                throw new InvalidOperationException("insufficient_amount_for_one_contract");

            var spent = Math.Round(contracts * price, 2, MidpointRounding.AwayFromZero);
            // No fee: net amount equals spent
            var net = spent;

            // Resolve account balance via wallet repository
            var account = await _walletRepo.GetAccountByUserAndCurrencyAsync(userId, "BRL", ct);
            if (account == null)
            {
                // Ensure account creation
                await _walletRepo.EnsureAccountsAsync(userId, "BRL", ct);
                account = await _walletRepo.GetAccountByUserAndCurrencyAsync(userId, "BRL", ct);
            }
            if (account == null)
                throw new InvalidOperationException("user_account_not_found");

            var balance = await _walletRepo.GetAccountBalanceAsync(account.Id, ct);
            if (balance < net)
                throw new InvalidOperationException("insufficient_funds");

            // Create ledger entry for deduction (atomicity ensured by transaction)
            var ledgerEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                Amount = -net,
                Type = LedgerEntryType.MARKET_BUY,
                CreatedAt = DateTimeOffset.UtcNow,
                ReferenceType = "market_trade",
                ReferenceId = marketId,
                IdempotencyKey = idempotencyKey,
            };
            _db.LedgerEntries.Add(ledgerEntry);

            // Position
            var pos = await _db.Positions.FirstOrDefaultAsync(
                p =>
                    p.UserId == userId
                    && p.MarketId == marketId
                    && p.Side == side
                    && p.Status == "open",
                ct
            );
            if (pos == null)
            {
                pos = new Position
                {
                    Id = Guid.NewGuid(),
                    MarketId = marketId,
                    UserId = userId,
                    Side = side,
                    Contracts = contracts,
                    AveragePrice = price,
                    TotalInvested = spent,
                };
                _db.Positions.Add(pos);
            }
            else
            {
                var totalContracts = pos.Contracts + contracts;
                pos.AveragePrice =
                    ((pos.AveragePrice * pos.Contracts) + (price * contracts)) / totalContracts;
                pos.Contracts = totalContracts;
                pos.TotalInvested += spent;
            }

            var txRecord = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = "buy",
                Amount = spent,
                NetAmount = net,
                MarketId = marketId,
                Description = $"Compra {contracts} contratos {(side == "yes" ? "sim" : "não")}",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _db.MarketTransactions.Add(txRecord);

            var fill = new PositionFill
            {
                Id = Guid.NewGuid(),
                PositionId = pos.Id,
                UserId = userId,
                MarketId = marketId,
                Side = side,
                Type = "BUY",
                Contracts = contracts,
                Price = price,
                GrossAmount = spent,
                FeeAmount = 0m,
                NetAmount = net,
                Source = "ORDER",
                OrderId = null,
                IdempotencyKey = idempotencyKey,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _db.PositionFills.Add(fill);

            market.VolumeTotal += spent;
            if (side == "yes")
                market.YesContracts += contracts;
            else
                market.NoContracts += contracts;

            var receiptPayload = new
            {
                tx = new
                {
                    txRecord.Id,
                    txRecord.UserId,
                    txRecord.Type,
                    txRecord.Amount,
                    txRecord.NetAmount,
                    txRecord.MarketId,
                    txRecord.Description,
                    txRecord.CreatedAt,
                },
                ledger = new
                {
                    ledgerEntry.Id,
                    ledgerEntry.AccountId,
                    ledgerEntry.Amount,
                    ledgerEntry.Type,
                    ledgerEntry.CreatedAt,
                    ledgerEntry.ReferenceType,
                    ledgerEntry.ReferenceId,
                    ledgerEntry.IdempotencyKey,
                },
                contracts,
                unitPrice = price,
                marketId,
                positionId = pos.Id,
            };

            var receipt = new Receipt
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = "buy",
                Amount = ledgerEntry.Amount,
                Currency = "BRL",
                Provider = "INTERNAL",
                MarketId = market.Id,
                MarketTitleSnapshot = market.Title,
                MarketSlugSnapshot = null,
                Description = txRecord.Description,
                ReferenceType = "LedgerEntry",
                ReferenceId = ledgerEntry.Id,
                PayloadJson = JsonSerializer.Serialize(receiptPayload),
            };
            _db.Receipts.Add(receipt);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var newBalance = await _walletRepo.GetAccountBalanceAsync(account.Id, ct);
            var result = new BuyResponse
            {
                Contracts = contracts,
                PricePerContract = price,
                Spent = spent,
                Fee = 0m,
                NewBalance = newBalance,
                PositionId = pos.Id,
            };

            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                _db.IdempotencyRecords.Add(
                    new IdempotencyRecord
                    {
                        Id = Guid.NewGuid(),
                        Key = idempotencyKey,
                        UserId = userId,
                        ResultPayload = JsonSerializer.Serialize(result),
                    }
                );
                await _db.SaveChangesAsync(ct);
            }

            await _notifier.NotifyMarketUpdated(
                marketId,
                new
                {
                    marketId = market.Id,
                    yesPrice = market.YesPrice,
                    noPrice = market.NoPrice,
                    volumeTotal = market.VolumeTotal,
                },
                ct
            );

            await _notifier.NotifyUserBalanceUpdated(userId, result.NewBalance, ct);

            return result;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<MarketResponse> CreateMarketAsync(
        CreateMarketRequest req,
        Guid? userId,
        string? userEmail,
        bool isAdminL2,
        string? idempotencyKey,
        bool confirmLowLiquidity,
        CancellationToken ct = default
    )
    {
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await _marketRepo.GetIdempotencyRecordAsync(
                idempotencyKey,
                userId ?? Guid.Empty,
                ct
            );
            if (existing != null)
            {
                return JsonSerializer.Deserialize<MarketResponse>(existing.ResultPayload)!;
            }
        }

        // Normalize tags
        string? tagsSerialized = null;
        if (req.Tags != null && req.Tags.Count > 0)
        {
            var normalized = req
                .Tags.Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant().Replace(' ', '-'))
                .Distinct();
            tagsSerialized = string.Join(',', normalized);
        }

        var yesPrice = Math.Round(req.Probability / 100m, 2);
        var noPrice = Math.Round(1m - yesPrice, 2);

        var needsLowLiquidityConfirm = req.Probability <= 5 || req.Probability >= 95;

        var market = new Market
        {
            Id = Guid.NewGuid(),
            Title = Sanitize(req.Title),
            Description = Sanitize(req.Description),
            Category = string.IsNullOrWhiteSpace(req.Category)
                ? null
                : NormalizeCategory(req.Category),
            Tags = tagsSerialized,
            YesPrice = yesPrice,
            NoPrice = noPrice,
            YesContracts = 0,
            NoContracts = 0,
            VolumeTotal = 0m,
            Volume24h = 0m,
            Volatility24h = 0m,
            ClosingDate = req.ClosingDate,
            ResolutionDate = req.ResolutionDate,
            ResolutionSource = req.ResolutionSource,
            Status = "open",
            Featured = req.Featured && isAdminL2,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = userId,
            CreatorEmail = userEmail,
            LowLiquidityWarning = needsLowLiquidityConfirm,
            ProbabilityBucket = ComputeProbabilityBucket(req.Probability),
            SearchSnippet = BuildSearchSnippet(req.Description),
        };

        // Delegate persistence to repository (handles transaction)
        IdempotencyRecord? idemRecord = null;
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var payload = JsonSerializer.Serialize(
                new MarketResponse
                {
                    Id = market.Id,
                    Title = market.Title,
                    Description = market.Description,
                    Category = market.Category,
                    ClosingDate = market.ClosingDate,
                    ResolutionSource = market.ResolutionSource,
                    Status = market.Status,
                    Featured = market.Featured,
                    YesPrice = market.YesPrice,
                    NoPrice = market.NoPrice,
                    VolumeTotal = market.VolumeTotal,
                    YesContracts = market.YesContracts,
                    NoContracts = market.NoContracts,
                }
            );

            idemRecord = new IdempotencyRecord
            {
                Id = Guid.NewGuid(),
                Key = idempotencyKey,
                UserId = userId ?? Guid.Empty,
                ResultPayload = payload,
                CreatedAt = DateTimeOffset.UtcNow,
            };
        }

        await _marketRepo.CreateMarketWithIdempotencyAsync(market, idemRecord, ct);
        // Associate categories (create defaults and link this market)
        try
        {
            await _categoryService.AssociateCategoryWithMarketAsync(market, ct);
        }
        catch
        {
            // swallow category association errors to avoid breaking market creation
        }

        try
        {
            await _notifier.NotifyMarketUpdated(
                market.Id,
                new
                {
                    id = market.Id,
                    title = market.Title,
                    category = market.Category,
                    yes_price = market.YesPrice,
                    no_price = market.NoPrice,
                    closing_date = market.ClosingDate,
                    featured = market.Featured,
                },
                ct
            );
        }
        catch
        {
            // swallow notifier errors
        }

        return new MarketResponse
        {
            Id = market.Id,
            Title = market.Title,
            Description = market.Description,
            Category = market.Category,
            ClosingDate = market.ClosingDate,
            ResolutionSource = market.ResolutionSource,
            Status = market.Status,
            Featured = market.Featured,
            YesPrice = market.YesPrice,
            NoPrice = market.NoPrice,
            VolumeTotal = market.VolumeTotal,
            YesContracts = market.YesContracts,
            NoContracts = market.NoContracts,
        };
    }

    public async Task<MarketResponse> UpdateMarketAsync(
        Guid marketId,
        UpdateMarketRequest req,
        Guid? userId,
        bool isAdminL2,
        CancellationToken ct = default
    )
    {
        var market = await _marketRepo.GetByIdAsync(marketId, ct);
        if (market == null)
            throw new InvalidOperationException("market_not_found");

        if (!string.Equals(market.Status, "open", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("market_not_editable");

        string? tagsSerialized = null;
        if (req.Tags != null && req.Tags.Count > 0)
        {
            var normalized = req
                .Tags.Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant().Replace(' ', '-'))
                .Distinct();
            tagsSerialized = string.Join(',', normalized);
        }

        var yesPrice = Math.Round(req.Probability / 100m, 2);
        var noPrice = Math.Round(1m - yesPrice, 2);

        var needsLowLiquidityConfirm = req.Probability <= 5 || req.Probability >= 95;

        market.Title = Sanitize(req.Title);
        market.Description = Sanitize(req.Description);
        market.Category = string.IsNullOrWhiteSpace(req.Category)
            ? null
            : NormalizeCategory(req.Category);
        market.Tags = tagsSerialized;
        market.YesPrice = yesPrice;
        market.NoPrice = noPrice;
        market.ClosingDate = req.ClosingDate;
        market.ResolutionDate = req.ResolutionDate;
        market.ResolutionSource = req.ResolutionSource;
        market.Featured = req.Featured && isAdminL2;
        market.LowLiquidityWarning = needsLowLiquidityConfirm;
        market.ProbabilityBucket = ComputeProbabilityBucket(req.Probability);
        market.SearchSnippet = BuildSearchSnippet(req.Description);
        market.UpdatedAt = DateTimeOffset.UtcNow;

        await _marketRepo.UpdateAsync(market, ct);

        try
        {
            await _notifier.NotifyMarketUpdated(
                market.Id,
                new
                {
                    id = market.Id,
                    title = market.Title,
                    category = market.Category,
                    yes_price = market.YesPrice,
                    no_price = market.NoPrice,
                    closing_date = market.ClosingDate,
                    featured = market.Featured,
                },
                ct
            );
        }
        catch
        {
            // swallow notifier errors
        }

        return new MarketResponse
        {
            Id = market.Id,
            Title = market.Title,
            Description = market.Description,
            Category = market.Category,
            ClosingDate = market.ClosingDate,
            ResolutionSource = market.ResolutionSource,
            Status = market.Status,
            Featured = market.Featured,
            YesPrice = market.YesPrice,
            NoPrice = market.NoPrice,
            VolumeTotal = market.VolumeTotal,
            YesContracts = market.YesContracts,
            NoContracts = market.NoContracts,
        };
    }

    public async Task DeleteMarketAsync(Guid marketId, Guid? userId, CancellationToken ct = default)
    {
        var market = await _marketRepo.GetByIdAsync(marketId, ct);
        if (market == null)
            throw new InvalidOperationException("market_not_found");

        // Soft-delete by marking status as deleted
        market.Status = "deleted";
        await _marketRepo.UpdateAsync(market, ct);

        try
        {
            await _notifier.NotifyMarketUpdated(
                marketId,
                new { id = market.Id, status = market.Status },
                ct
            );
        }
        catch
        {
            // swallow notifier issues
        }
    }

    private decimal CalculateFee(decimal amount)
    {
        // default 0.5% fee
        var fee = Math.Round(amount * 0.005m, 2);
        return Math.Max(fee, 0.01m);
    }

    // Helpers reused from controller refactor
    private static string Sanitize(string input)
    {
        return string.Concat(input.Where(c => !char.IsControl(c))).Trim();
    }

    private static string ComputeProbabilityBucket(int p)
    {
        var lower = (p / 10) * 10;
        var upper = lower + 9;
        if (lower < 0)
            lower = 0;
        if (upper > 99)
            upper = 99;
        return $"{lower}-{upper}";
    }

    private static string BuildSearchSnippet(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;
        var snippet =
            description.Length <= 140 ? description : description.Substring(0, 137) + "...";
        return snippet;
    }

    private static string NormalizeCategory(string category)
    {
        var normalized = RemoveDiacritics(category).Trim().ToUpperInvariant();
        normalized = Regex.Replace(normalized, "\\s+", " ");
        return normalized;
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
