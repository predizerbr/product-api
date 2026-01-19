using Microsoft.AspNetCore.Http;
using Product.Business.Interfaces.Results;
using Product.Contracts.Users.PaymentsMethods.Card;
using Product.Contracts.Users.PaymentsMethods.Pix;

namespace Product.Business.Interfaces.Payments;

public interface IMercadoPagoService
{
    Task<ApiResult> CreateCardOrderAsync(
        CreateCardOrderRequest request,
        string? deviceId,
        CancellationToken ct = default
    );

    Task<ApiResult> GetOrderStatusAsync(string orderIdOrMpId, CancellationToken ct = default);

    Task<ApiResult> CreatePixAsync(
        CreatePixRequest request,
        string? deviceId,
        CancellationToken ct = default
    );

    Task<ApiResult> GetPaymentStatusAsync(long paymentId, CancellationToken ct = default);

    Task<ApiResult> SaveCardAsync(
        SaveCardRequest request,
        string? deviceId,
        CancellationToken ct = default
    );

    Task<ApiResult> HandleWebhookAsync(HttpRequest request, CancellationToken ct = default);
}
