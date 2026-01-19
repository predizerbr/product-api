using Product.Data.Models.Orders;

namespace Product.Business.Interfaces.Payments;

public interface IOrderService
{
    Task<Order?> GetByExternalIdAsync(string externalOrderId, CancellationToken ct = default);

    Task<Order> CreateOrUpdateAsync(
        string externalOrderId,
        decimal amount,
        string currency,
        string provider,
        long? providerPaymentId,
        string? providerPaymentIdText,
        string status,
        string? statusDetail,
        string paymentMethod,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default
    );

    Task<Order?> UpdateStatusAsync(
        string externalOrderId,
        string status,
        string? statusDetail,
        long? providerPaymentId,
        string? providerPaymentIdText = null,
        CancellationToken ct = default
    );

    Task<Order?> UpdateStatusByProviderIdAsync(
        long providerPaymentId,
        string status,
        string? statusDetail,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default
    );

    Task<Order?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    );
}
