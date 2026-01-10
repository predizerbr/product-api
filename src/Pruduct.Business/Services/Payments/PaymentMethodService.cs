using Microsoft.EntityFrameworkCore;
using Pruduct.Business.Interfaces.Payments;
using Pruduct.Business.Interfaces.Results;
using Pruduct.Common.Enums;
using Pruduct.Contracts.Payments;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models.Payments;

namespace Pruduct.Business.Services.Payments;

public class PaymentMethodService : IPaymentMethodService
{
    private readonly AppDbContext _db;

    public PaymentMethodService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<PaymentMethodListResponse>> GetMethodsAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        var methods = await _db
            .PaymentMethods.Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var response = new PaymentMethodListResponse { Items = methods.Select(Map).ToList() };

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

        if (type == PaymentMethodType.CARD)
        {
            if (
                string.IsNullOrWhiteSpace(request.CardBrand)
                || string.IsNullOrWhiteSpace(request.CardLast4)
                || request.CardExpMonth is null
                || request.CardExpYear is null
                || string.IsNullOrWhiteSpace(request.CardHolderName)
            )
            {
                return ServiceResult<PaymentMethodResponse>.Fail("card_required");
            }
        }

        if (type == PaymentMethodType.BANK_ACCOUNT)
        {
            if (
                string.IsNullOrWhiteSpace(request.BankCode)
                || string.IsNullOrWhiteSpace(request.Agency)
                || string.IsNullOrWhiteSpace(request.AccountNumber)
            )
            {
                return ServiceResult<PaymentMethodResponse>.Fail("bank_account_required");
            }
        }

        var hasMethods = await _db.PaymentMethods.AnyAsync(x => x.UserId == userId, ct);
        var isDefault = request.IsDefault ?? !hasMethods;
        if (isDefault)
        {
            var existingDefaults = await _db
                .PaymentMethods.Where(x => x.UserId == userId && x.IsDefault)
                .ToListAsync(ct);

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }
        }

        var method = new PaymentMethod
        {
            UserId = userId,
            Type = type,
            IsDefault = isDefault,
            PixKey = request.PixKey,
            CardBrand = request.CardBrand,
            CardLast4 = request.CardLast4,
            CardExpMonth = request.CardExpMonth,
            CardExpYear = request.CardExpYear,
            CardHolderName = request.CardHolderName,
            BankCode = request.BankCode,
            BankName = request.BankName,
            Agency = request.Agency,
            AccountNumber = request.AccountNumber,
            AccountDigit = request.AccountDigit,
            AccountType = request.AccountType,
        };

        _db.PaymentMethods.Add(method);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<PaymentMethodResponse>.Ok(Map(method));
    }

    public async Task<ServiceResult<bool>> DeleteMethodAsync(
        Guid userId,
        Guid methodId,
        CancellationToken ct = default
    )
    {
        var method = await _db.PaymentMethods.FirstOrDefaultAsync(
            x => x.Id == methodId && x.UserId == userId,
            ct
        );
        if (method is null)
        {
            return ServiceResult<bool>.Fail("payment_method_not_found");
        }

        _db.PaymentMethods.Remove(method);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    private static PaymentMethodResponse Map(PaymentMethod method) =>
        new()
        {
            Id = method.Id,
            Type = method.Type.ToString(),
            IsDefault = method.IsDefault,
            CreatedAt = method.CreatedAt,
            PixKey = method.PixKey,
            CardBrand = method.CardBrand,
            CardLast4 = method.CardLast4,
            CardExpMonth = method.CardExpMonth,
            CardExpYear = method.CardExpYear,
            CardHolderName = method.CardHolderName,
            BankCode = method.BankCode,
            BankName = method.BankName,
            Agency = method.Agency,
            AccountNumber = method.AccountNumber,
            AccountDigit = method.AccountDigit,
            AccountType = method.AccountType,
        };
}
