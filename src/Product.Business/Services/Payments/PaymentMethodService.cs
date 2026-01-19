using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Product.Business.Interfaces.Payments;
using Product.Business.Interfaces.Results;
using Product.Common.Enums;
using Product.Contracts.Users.PaymentsMethods;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Users.PaymentsMethods;

namespace Product.Business.Services.Payments;

public class PaymentMethodService(IPaymentMethodRepository paymentMethodRepository)
    : IPaymentMethodService
{
    private readonly IPaymentMethodRepository _paymentMethodRepository = paymentMethodRepository;

    public async Task<ApiResult> GetMethodsApiAsync(
        ClaimsPrincipal principal,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var (cards, banks, pix) = await _paymentMethodRepository.GetByUserAsync(userId, ct);

        var serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };
        var items = new JArray();

        foreach (var card in cards)
        {
            var obj = new
            {
                id = card.Id,
                type = PaymentMethodType.CARD.ToString(),
                isDefault = card.IsDefault,
                createdAt = card.CreatedAt,
                cardBrand = card.CardBrand,
                cardLast4 = card.CardLast4,
                cardExpMonth = card.CardExpMonth,
                cardExpYear = card.CardExpYear,
                cardHolderName = card.CardHolderName,
                mpCustomerId = card.MpCustomerId,
                mpCardId = card.MpCardId,
                mpPaymentMethodId = card.MpPaymentMethodId,
                cardHolderDocumentType = card.CardHolderDocumentType,
                cardHolderDocumentNumber = card.CardHolderDocumentNumber,
                cardHolderDocumentLast4 = card.CardHolderDocumentLast4,
            };
            items.Add(JObject.FromObject(obj, serializer));
        }

        foreach (var bank in banks)
        {
            var obj = new
            {
                id = bank.Id,
                type = PaymentMethodType.BANK_ACCOUNT.ToString(),
                isDefault = bank.IsDefault,
                createdAt = bank.CreatedAt,
                bankCode = bank.BankCode,
                bankName = bank.BankName,
                agency = bank.Agency,
                accountNumber = bank.AccountNumber,
                accountDigit = bank.AccountDigit,
                accountType = bank.AccountType,
            };
            items.Add(JObject.FromObject(obj, serializer));
        }

        foreach (var p in pix)
        {
            var obj = new
            {
                id = p.Id,
                type = PaymentMethodType.PIX.ToString(),
                isDefault = p.IsDefault,
                createdAt = p.CreatedAt,
                pixKey = p.PixKey,
            };
            items.Add(JObject.FromObject(obj, serializer));
        }

        var body = new { items };
        return ApiResult.Ok(body, envelope: true);
    }

    public async Task<ApiResult> CreateMethodApiAsync(
        ClaimsPrincipal principal,
        CreatePaymentMethodRequest request,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await CreateMethodAsync(userId, request, ct);
        if (!result.Success)
        {
            var status = result.Error switch
            {
                "invalid_payment_type" => StatusCodes.Status400BadRequest,
                "pix_key_required" => StatusCodes.Status400BadRequest,
                "card_required" => StatusCodes.Status400BadRequest,
                "bank_account_required" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiResult.Problem(status, result.Error ?? "unknown");
        }

        return ApiResult.Ok(result.Data, envelope: true);
    }

    public async Task<ApiResult> DeleteMethodApiAsync(
        ClaimsPrincipal principal,
        Guid methodId,
        CancellationToken ct = default
    )
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return ApiResult.Problem(StatusCodes.Status401Unauthorized, "invalid_token");
        }

        var result = await DeleteMethodAsync(userId, methodId, ct);
        if (!result.Success)
        {
            var status =
                result.Error == "payment_method_not_found"
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;
            return ApiResult.Problem(status, result.Error ?? "unknown");
        }

        return ApiResult.Ok(true, envelope: true);
    }

    public async Task<ServiceResult<PaymentMethodListResponse>> GetMethodsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var (cards, banks, pix) = await _paymentMethodRepository.GetByUserAsync(userId, ct);

        var items = new List<PaymentMethodResponse>();
        items.AddRange(cards.Select(Map));
        items.AddRange(banks.Select(Map));
        items.AddRange(pix.Select(Map));

        var response = new PaymentMethodListResponse { Items = items };

        return ServiceResult<PaymentMethodListResponse>.Ok(response);
    }

    public async Task<ServiceResult<PaymentMethodResponse>> CreateMethodAsync(
        Guid userId,
        CreatePaymentMethodRequest request,
        CancellationToken ct = default
    )
    {
        if (!Enum.TryParse<PaymentMethodType>(request.Type, true, out var type))
        {
            return ServiceResult<PaymentMethodResponse>.Fail("invalid_payment_type");
        }

        if (type == PaymentMethodType.PIX && string.IsNullOrWhiteSpace(request.PixKey))
        {
            return ServiceResult<PaymentMethodResponse>.Fail("pix_key_required");
        }

        if (
            type == PaymentMethodType.CARD
            && (
                string.IsNullOrWhiteSpace(request.CardBrand)
                || string.IsNullOrWhiteSpace(request.CardLast4)
                || request.CardExpMonth is null
                || request.CardExpYear is null
                || string.IsNullOrWhiteSpace(request.CardHolderName)
            )
        )
        {
            return ServiceResult<PaymentMethodResponse>.Fail("card_required");
        }

        if (
            type == PaymentMethodType.BANK_ACCOUNT
            && (
                string.IsNullOrWhiteSpace(request.BankCode)
                || string.IsNullOrWhiteSpace(request.Agency)
                || string.IsNullOrWhiteSpace(request.AccountNumber)
            )
        )
        {
            return ServiceResult<PaymentMethodResponse>.Fail("bank_account_required");
        }

        var hasMethods = await _paymentMethodRepository.HasAnyAsync(userId, ct);
        var isDefault = request.IsDefault ?? !hasMethods;

        UserCard? createdCard = null;
        UserBankAccount? createdBank = null;
        UserPixKey? createdPix = null;
        var cardHolderDocumentType = NormalizeDocumentType(request.HolderIdentification?.Type);
        var cardHolderDocumentNumber = OnlyDigits(request.HolderIdentification?.Number);
        if (string.IsNullOrWhiteSpace(cardHolderDocumentNumber))
            cardHolderDocumentNumber = null;

        var cardHolderDocumentLast4 =
            cardHolderDocumentNumber?.Length >= 4 ? cardHolderDocumentNumber[^4..] : null;

        switch (type)
        {
            case PaymentMethodType.PIX:
                createdPix = new UserPixKey
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PixKey = request.PixKey,
                    IsDefault = isDefault,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await _paymentMethodRepository.AddUserPixKeyAsync(createdPix, ct);
                break;

            case PaymentMethodType.CARD:
                createdCard = new UserCard
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CardBrand = request.CardBrand,
                    CardLast4 = request.CardLast4,
                    CardExpMonth = request.CardExpMonth,
                    CardExpYear = request.CardExpYear,
                    CardHolderName = request.CardHolderName,
                    MpCustomerId = request.MpCustomerId,
                    MpCardId = request.MpCardId,
                    MpPaymentMethodId = request.MpPaymentMethodId,
                    CardHolderDocumentType = cardHolderDocumentType,
                    CardHolderDocumentNumber = cardHolderDocumentNumber,
                    CardHolderDocumentLast4 = cardHolderDocumentLast4,
                    IsDefault = isDefault,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await _paymentMethodRepository.AddUserCardAsync(createdCard, ct);
                break;

            case PaymentMethodType.BANK_ACCOUNT:
                createdBank = new UserBankAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    BankCode = request.BankCode,
                    BankName = request.BankName,
                    Agency = request.Agency,
                    AccountNumber = request.AccountNumber,
                    AccountDigit = request.AccountDigit,
                    AccountType = request.AccountType,
                    IsDefault = isDefault,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await _paymentMethodRepository.AddUserBankAccountAsync(createdBank, ct);
                break;

            default:
                return ServiceResult<PaymentMethodResponse>.Fail("invalid_payment_type");
        }

        await _paymentMethodRepository.SaveChangesAsync(ct);

        if (createdCard is not null)
        {
            return ServiceResult<PaymentMethodResponse>.Ok(Map(createdCard));
        }
        else if (createdBank is not null)
        {
            return ServiceResult<PaymentMethodResponse>.Ok(Map(createdBank));
        }
        else if (createdPix is not null)
        {
            return ServiceResult<PaymentMethodResponse>.Ok(Map(createdPix));
        }

        return ServiceResult<PaymentMethodResponse>.Fail("invalid_payment_type");
    }

    public async Task<ServiceResult<bool>> DeleteMethodAsync(
        Guid userId,
        Guid methodId,
        CancellationToken ct = default
    )
    {
        var (card, bank, pix) = await _paymentMethodRepository.GetByIdAsync(userId, methodId, ct);
        if (card is null && bank is null && pix is null)
        {
            return ServiceResult<bool>.Fail("payment_method_not_found");
        }

        await _paymentMethodRepository.RemoveByIdAsync(userId, methodId, ct);

        return ServiceResult<bool>.Ok(true);
    }

    private static PaymentMethodResponse Map(UserCard card) =>
        new()
        {
            Id = card.Id,
            Type = PaymentMethodType.CARD.ToString(),
            IsDefault = card.IsDefault,
            CreatedAt = card.CreatedAt,
            CardBrand = card.CardBrand,
            CardLast4 = card.CardLast4,
            CardExpMonth = card.CardExpMonth,
            CardExpYear = card.CardExpYear,
            CardHolderName = card.CardHolderName,
            MpCustomerId = card.MpCustomerId,
            MpCardId = card.MpCardId,
            MpPaymentMethodId = card.MpPaymentMethodId,
            CardHolderDocumentType = card.CardHolderDocumentType,
            CardHolderDocumentNumber = card.CardHolderDocumentNumber,
            CardHolderDocumentLast4 = card.CardHolderDocumentLast4,
        };

    private static PaymentMethodResponse Map(UserBankAccount bank) =>
        new()
        {
            Id = bank.Id,
            Type = PaymentMethodType.BANK_ACCOUNT.ToString(),
            IsDefault = bank.IsDefault,
            CreatedAt = bank.CreatedAt,
            BankCode = bank.BankCode,
            BankName = bank.BankName,
            Agency = bank.Agency,
            AccountNumber = bank.AccountNumber,
            AccountDigit = bank.AccountDigit,
            AccountType = bank.AccountType,
        };

    private static PaymentMethodResponse Map(UserPixKey pix) =>
        new()
        {
            Id = pix.Id,
            Type = PaymentMethodType.PIX.ToString(),
            IsDefault = pix.IsDefault,
            CreatedAt = pix.CreatedAt,
            PixKey = pix.PixKey,
        };

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static string? NormalizeDocumentType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return null;

        var normalized = type.Trim().ToUpperInvariant();
        return normalized is "CPF" or "CNPJ" ? normalized : null;
    }

    private static string OnlyDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).ToArray());
    }
}
