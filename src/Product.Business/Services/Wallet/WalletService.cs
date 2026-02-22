using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Product.Business.Interfaces.Payments;
using Product.Business.Interfaces.Results;
using Product.Business.Interfaces.Wallet;
using Product.Common.Enums;
using Product.Contracts.Users.PaymentsMethods;
using Product.Contracts.Users.PaymentsMethods.Pix;
using Product.Contracts.Wallet;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Wallet;

namespace Product.Business.Services.Wallet;

public class WalletService : IWalletService
{
    private const string DefaultCurrency = "BRL";
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    private readonly IWalletRepository _walletRepository;
    private readonly IMercadoPagoService _mercadoPagoService;
    private readonly IUserRepository _userRepository;
    private readonly IReceiptService _receiptService;

    public WalletService(
        IWalletRepository walletRepository,
        IMercadoPagoService mercadoPagoService,
        IUserRepository userRepository,
        IReceiptService receiptService
    )
    {
        _walletRepository = walletRepository;
        _mercadoPagoService = mercadoPagoService;
        _userRepository = userRepository;
        _receiptService = receiptService;
    }

    public async Task<ApiResult> GetBalancesApiAsync(
        ClaimsPrincipal principal,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetBalancesAsync(userId, ct);
        if (!result.Success)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> GetSummaryApiAsync(
        ClaimsPrincipal principal,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetSummaryAsync(userId, ct);
        if (!result.Success)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> GetLedgerApiAsync(
        ClaimsPrincipal principal,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetLedgerAsync(userId, cursor, limit, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "invalid_cursor"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
            return ApiResult.Problem(status, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> CreateDepositIntentApiAsync(
        ClaimsPrincipal principal,
        IHeaderDictionary headers,
        AmountRequest request,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        if (!TryGetIdempotencyKey(headers, out var idempotencyKey))
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, "idempotency_required");
        }

        var result = await CreateDepositIntentAsync(userId, request, idempotencyKey, ct);
        if (!result.Success)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> GetDepositsApiAsync(
        ClaimsPrincipal principal,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetDepositsAsync(userId, cursor, limit, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "invalid_cursor"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
            return ApiResult.Problem(status, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> CreateWithdrawalApiAsync(
        ClaimsPrincipal principal,
        IHeaderDictionary headers,
        AmountRequest request,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        if (!TryGetIdempotencyKey(headers, out var idempotencyKey))
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, "idempotency_required");
        }

        var result = await CreateWithdrawalAsync(userId, request, idempotencyKey, ct);
        if (!result.Success)
        {
            return ApiResult.Problem(StatusCodes.Status400BadRequest, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> GetWithdrawalsApiAsync(
        ClaimsPrincipal principal,
        string? cursor,
        int? limit,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await GetWithdrawalsAsync(userId, cursor, limit, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "invalid_cursor"
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status404NotFound;
            return ApiResult.Problem(status, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> ApproveWithdrawalApiAsync(
        ClaimsPrincipal principal,
        Guid withdrawalId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var adminUserId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await ApproveWithdrawalAsync(withdrawalId, adminUserId, request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "withdrawal_not_found" => StatusCodes.Status404NotFound,
                "invalid_status" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiResult.Problem(status, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> RejectWithdrawalApiAsync(
        ClaimsPrincipal principal,
        Guid withdrawalId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var adminUserId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await RejectWithdrawalAsync(withdrawalId, adminUserId, request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "withdrawal_not_found" => StatusCodes.Status404NotFound,
                "invalid_status" => StatusCodes.Status400BadRequest,
                "account_not_found" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiResult.Problem(status, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ServiceResult<bool>> ConfirmDepositAsync(
        Guid paymentIntentId,
        string providerPaymentId,
        CancellationToken ct = default
    )
    {
        decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        var intent = await _walletRepository.GetPaymentIntentByIdAsync(paymentIntentId, ct);
        if (intent is null)
            return ServiceResult<bool>.Fail("payment_intent_not_found");

        if (intent.Status == PaymentIntentStatus.APPROVED)
        {
            await _receiptService.EnsureDepositReceiptAsync(intent.Id, ct);
            return ServiceResult<bool>.Ok(true); // idempotent
        }

        // marca como aprovado e cria ledger entry
        var accounts = await EnsureAccountsAsync(intent.UserId, ct);
        var account = accounts[0];

        var entry = new LedgerEntry
        {
            AccountId = account.Id,
            Type = LedgerEntryType.DEPOSIT_GATEWAY,
            Amount = Round2(intent.Amount),
            ReferenceType = "PaymentIntent",
            ReferenceId = intent.Id,
            IdempotencyKey = providerPaymentId ?? intent.IdempotencyKey,
        };

        intent.Status = PaymentIntentStatus.APPROVED;
        intent.ExternalPaymentId ??= providerPaymentId;

        await _walletRepository.AddLedgerEntryAsync(entry, ct);
        await _receiptService.EnsureDepositReceiptAsync(intent.Id, ct);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> SyncDepositStatusAsync(
        Guid paymentIntentId,
        string? providerStatus,
        string? providerStatusDetail,
        string? providerPaymentId,
        decimal? providerAmount,
        CancellationToken ct = default
    )
    {
        var intent = await _walletRepository.GetPaymentIntentByIdAsync(paymentIntentId, ct);
        if (intent is null)
        {
            return ServiceResult<bool>.Fail("payment_intent_not_found");
        }

        decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
        var amountAdjusted = false;
        if (providerAmount is > 0)
        {
            if (intent.Status != PaymentIntentStatus.APPROVED)
            {
                var rounded = Round2(providerAmount.Value);
                if (intent.Amount != rounded)
                {
                    intent.Amount = rounded;
                    amountAdjusted = true;
                }
            }
            else
            {
                var rounded = Round2(providerAmount.Value);
                if (intent.Amount != rounded)
                {
                    intent.Amount = rounded;
                    amountAdjusted = true;
                }

                var ledger = await _walletRepository.GetLedgerEntryByReferenceAsync(
                    "PaymentIntent",
                    intent.Id,
                    ct
                );
                if (ledger is not null && ledger.Amount != rounded)
                {
                    ledger.Amount = rounded;
                    amountAdjusted = true;
                }
            }
        }

        var mappedStatus = MapMpStatusToIntentStatus(providerStatus, providerStatusDetail);
        var isCredited = IsCredited(providerStatus, providerStatusDetail);
        if (mappedStatus == PaymentIntentStatus.APPROVED || isCredited)
        {
            if (intent.Status == PaymentIntentStatus.APPROVED)
            {
                if (amountAdjusted)
                    await _walletRepository.UpdatePaymentIntentAsync(intent, ct);
                await _receiptService.EnsureDepositReceiptAsync(intent.Id, ct);
                return ServiceResult<bool>.Ok(true);
            }

            return await ConfirmDepositAsync(
                paymentIntentId,
                providerPaymentId ?? intent.IdempotencyKey,
                ct
            );
        }

        if (mappedStatus is null)
        {
            if (
                !string.IsNullOrWhiteSpace(providerPaymentId)
                && string.IsNullOrWhiteSpace(intent.ExternalPaymentId)
            )
            {
                intent.ExternalPaymentId = providerPaymentId;
                amountAdjusted = true;
            }

            if (amountAdjusted)
                await _walletRepository.UpdatePaymentIntentAsync(intent, ct);

            return ServiceResult<bool>.Ok(true);
        }

        if (intent.Status == PaymentIntentStatus.APPROVED)
        {
            if (amountAdjusted)
                await _walletRepository.UpdatePaymentIntentAsync(intent, ct);
            return ServiceResult<bool>.Ok(true);
        }

        intent.Status = mappedStatus.Value;
        if (!string.IsNullOrWhiteSpace(providerPaymentId))
        {
            intent.ExternalPaymentId ??= providerPaymentId;
        }

        await _walletRepository.UpdatePaymentIntentAsync(intent, ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<IReadOnlyCollection<WalletBalanceResponse>>> GetBalancesAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var accounts = await EnsureAccountsAsync(userId, ct);
        var accountIds = accounts.Select(a => a.Id).ToArray();

        var balanceLookup = await _walletRepository.GetLedgerBalancesAsync(accountIds, ct);

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

    public async Task<ServiceResult<WalletSummaryResponse>> GetSummaryAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var accounts = await EnsureAccountsAsync(userId, ct);
        var primaryCurrency =
            accounts.FirstOrDefault(a => a.Currency == DefaultCurrency)?.Currency ?? DefaultCurrency;

        var totalDeposited = await _walletRepository.GetTotalDepositedAsync(
            userId,
            primaryCurrency,
            ct
        );
        var totalWithdrawn = await _walletRepository.GetTotalWithdrawnAsync(
            userId,
            primaryCurrency,
            ct
        );
        var totalBought = await _walletRepository.GetTotalBoughtAsync(userId, ct);

        var response = new WalletSummaryResponse
        {
            Currency = primaryCurrency,
            TotalDeposited = Math.Round(totalDeposited, 2, MidpointRounding.AwayFromZero),
            TotalWithdrawn = Math.Round(totalWithdrawn, 2, MidpointRounding.AwayFromZero),
            TotalBought = Math.Round(totalBought, 2, MidpointRounding.AwayFromZero),
        };

        return ServiceResult<WalletSummaryResponse>.Ok(response);
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

        var entries = await _walletRepository.GetLedgerEntriesAsync(
            accountIds,
            cursorTime,
            pageSize + 1,
            ct
        );

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
        var amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0)
        {
            return ServiceResult<CreateDepositResponse>.Fail("invalid_amount");
        }

        var accounts = await EnsureAccountsAsync(userId, ct);
        var account = accounts[0];

        var existing = await _walletRepository.GetPaymentIntentByIdempotencyAsync(
            userId,
            idempotencyKey,
            ct
        );
        if (existing is not null)
        {
            return ServiceResult<CreateDepositResponse>.Ok(MapDeposit(existing));
        }

        var intent = new PaymentIntent
        {
            UserId = userId,
            Provider = "MERCADOPAGO",
            PaymentMethod = "pix",
            Amount = amount,
            Currency = account.Currency,
            Status = PaymentIntentStatus.PENDING,
            IdempotencyKey = idempotencyKey,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
        };

        await _walletRepository.AddPaymentIntentAsync(intent, ct);

        intent.ExternalReference = $"WALLET_DEPOSIT_{intent.Id}";
        await _walletRepository.UpdatePaymentIntentAsync(intent, ct);

        // Prepare PIX order on Mercado Pago
        var user = await _userRepository.GetUserByIdAsync(userId, ct);
        var email = user?.Email ?? "user@unknown.local";
        var payerId = user?.PersonalData?.Cpf;

        var pixReq = new CreatePixRequest
        {
            OrderId = intent.ExternalReference,
            Amount = amount,
            Description = "Wallet deposit",
            BuyerEmail = email,
            Payer = new PixPayer
            {
                Email = email,
                Identification = string.IsNullOrWhiteSpace(payerId)
                    ? null
                    : new Identification
                    {
                        Type = payerId!.Length > 11 ? "CNPJ" : "CPF",
                        Number = payerId,
                    },
            },
            ExpirationMinutes = 15,
        };

        var pixResult = await _mercadoPagoService.CreatePixAsync(pixReq, null, ct);
        if (pixResult.StatusCode >= StatusCodes.Status400BadRequest || pixResult.Data is null)
        {
            return ServiceResult<CreateDepositResponse>.Fail(
                pixResult.Error ?? "gateway_unavailable"
            );
        }

        if (pixResult.Data is PixResponse pix)
        {
            intent.ExternalPaymentId = pix.PaymentId.ToString();
            intent.PixQrCode = pix.QrCode;
            intent.PixQrCodeBase64 = pix.QrCodeBase64;
            intent.ExpiresAt = pix.ExpiresAt ?? intent.ExpiresAt;
            await _walletRepository.UpdatePaymentIntentAsync(intent, ct);
        }

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

        var intents = await _walletRepository.GetPaymentIntentsAsync(
            userId,
            cursorTime,
            pageSize + 1,
            ct
        );

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

        var existing = await _walletRepository.GetWithdrawalByIdempotencyAsync(
            userId,
            idempotencyKey,
            ct
        );
        if (existing is not null)
        {
            return ServiceResult<WithdrawalResponse>.Ok(MapWithdrawal(existing));
        }

        var balance = await _walletRepository.GetAccountBalanceAsync(account.Id, ct);

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

        await _walletRepository.AddWithdrawalAsync(withdrawal, ct);

        await _walletRepository.AddLedgerEntryAsync(
            new LedgerEntry
            {
                AccountId = account.Id,
                Type = LedgerEntryType.WITHDRAW_REQUEST,
                Amount = -request.Amount,
                ReferenceType = "Withdrawal",
                ReferenceId = withdrawal.Id,
                IdempotencyKey = idempotencyKey,
            },
            ct
        );

        // Persist immutable receipt for withdrawal request
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = "withdraw_request",
            Amount = -request.Amount,
            Currency = account.Currency,
            Description = "Solicitação de saque",
            ReferenceType = "Withdrawal",
            ReferenceId = withdrawal.Id,
            PayloadJson = JsonSerializer.Serialize(new { withdrawal = withdrawal }),
        };
        await _walletRepository.AddReceiptAsync(receipt, ct);

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

        var withdrawals = await _walletRepository.GetWithdrawalsAsync(
            userId,
            cursorTime,
            pageSize + 1,
            ct
        );

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
        var withdrawal = await _walletRepository.GetWithdrawalByIdAsync(withdrawalId, ct);
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

        await _walletRepository.UpdateWithdrawalAsync(withdrawal, ct);

        return ServiceResult<WithdrawalResponse>.Ok(MapWithdrawal(withdrawal));
    }

    public async Task<ServiceResult<WithdrawalResponse>> RejectWithdrawalAsync(
        Guid withdrawalId,
        Guid adminUserId,
        WithdrawalDecisionRequest request,
        CancellationToken ct = default
    )
    {
        var withdrawal = await _walletRepository.GetWithdrawalByIdAsync(withdrawalId, ct);
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

        var account = await _walletRepository.GetAccountByUserAndCurrencyAsync(
            withdrawal.UserId,
            withdrawal.Currency,
            ct
        );
        if (account is null)
        {
            return ServiceResult<WithdrawalResponse>.Fail("account_not_found");
        }

        await _walletRepository.AddLedgerEntryAsync(
            new LedgerEntry
            {
                AccountId = account.Id,
                Type = LedgerEntryType.WITHDRAW_REQUEST,
                Amount = withdrawal.Amount,
                ReferenceType = "Withdrawal",
                ReferenceId = withdrawal.Id,
                IdempotencyKey = $"withdraw-reject-{withdrawal.Id}",
            },
            ct
        );

        return ServiceResult<WithdrawalResponse>.Ok(MapWithdrawal(withdrawal));
    }

    private Task<List<Account>> EnsureAccountsAsync(Guid userId, CancellationToken ct) =>
        _walletRepository.EnsureAccountsAsync(userId, DefaultCurrency, ct);

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

    private static PaymentIntentStatus? MapMpStatusToIntentStatus(
        string? status,
        string? statusDetail
    )
    {
        if (string.Equals(statusDetail, "expired", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentIntentStatus.EXPIRED;
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
            "approved" => PaymentIntentStatus.APPROVED,
            "authorized" => PaymentIntentStatus.APPROVED,
            "paid" => PaymentIntentStatus.APPROVED,
            "processed" => PaymentIntentStatus.APPROVED,
            "failed" => PaymentIntentStatus.REJECTED,
            "rejected" => PaymentIntentStatus.REJECTED,
            "cancelled" => PaymentIntentStatus.EXPIRED,
            "canceled" => PaymentIntentStatus.EXPIRED,
            "expired" => PaymentIntentStatus.EXPIRED,
            _ => null,
        };
    }

    private static bool IsCredited(string? status, string? statusDetail)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            if (normalized is "approved" or "paid" or "processed" or "authorized")
                return true;
        }

        if (!string.IsNullOrWhiteSpace(statusDetail))
        {
            var sd = statusDetail.Trim().ToLowerInvariant();
            return sd.Contains("accredit")
                || sd.Contains("credited")
                || sd.Contains("paid")
                || sd.Contains("accredited")
                || sd.Contains("settled");
        }

        return false;
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
            ProviderPaymentId = long.TryParse(intent.ExternalPaymentId, out var pid) ? pid : null,
            ExternalReference = intent.ExternalReference,
            PixQrCode = intent.PixQrCode,
            PixQrCodeBase64 = intent.PixQrCodeBase64,
            CheckoutUrl = intent.CheckoutUrl,
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

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static bool TryGetIdempotencyKey(IHeaderDictionary headers, out string idempotencyKey)
    {
        idempotencyKey = string.Empty;
        if (!headers.TryGetValue("Idempotency-Key", out var values))
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
