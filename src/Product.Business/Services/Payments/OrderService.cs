using Product.Business.Interfaces.Payments;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Orders;

namespace Product.Business.Services.Payments;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;

    public OrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Order?> GetByExternalIdAsync(
        string externalOrderId,
        CancellationToken ct = default
    )
    {
        return await _orderRepository.GetByExternalIdAsync(externalOrderId, ct);
    }

    public async Task<Order> CreateOrUpdateAsync(
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
    )
    {
        var existing = await _orderRepository.GetByExternalIdAsync(externalOrderId, ct);
        if (existing is null)
        {
            var ord = new Order
            {
                OrderId = externalOrderId,
                Amount = amount,
                Currency = currency,
                Provider = provider,
                ProviderPaymentId = providerPaymentId,
                ProviderPaymentIdText = providerPaymentIdText,
                Status = status,
                StatusDetail = statusDetail,
                PaymentMethod = paymentMethod,
                ExpiresAtUtc = expiresAt?.ToUniversalTime(),
                Credited = false,
            };
            await _orderRepository.AddAsync(ord, ct);
            return ord;
        }

        // Idempotent update: if already approved, avoid downgrading
        if (existing.Status == "approved")
            return existing;

        existing.Amount = amount;
        existing.Currency = currency;
        existing.Provider = provider;
        existing.ProviderPaymentId = providerPaymentId ?? existing.ProviderPaymentId;
        existing.ProviderPaymentIdText = providerPaymentIdText ?? existing.ProviderPaymentIdText;
        existing.ExpiresAtUtc = expiresAt is not null
            ? expiresAt.Value.ToUniversalTime()
            : existing.ExpiresAtUtc;
        existing.Status = status;
        existing.StatusDetail = statusDetail ?? existing.StatusDetail;
        existing.PaymentMethod = paymentMethod;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _orderRepository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<Order?> UpdateStatusAsync(
        string externalOrderId,
        string status,
        string? statusDetail,
        long? providerPaymentId,
        string? providerPaymentIdText = null,
        CancellationToken ct = default
    )
    {
        var existing = await _orderRepository.GetByExternalIdAsync(externalOrderId, ct);
        if (existing is null)
            return null;

        if (existing.Status == "approved")
            return existing;

        existing.Status = status;
        existing.StatusDetail = statusDetail ?? existing.StatusDetail;
        if (providerPaymentId is not null)
            existing.ProviderPaymentId = providerPaymentId;
        if (!string.IsNullOrWhiteSpace(providerPaymentIdText))
            existing.ProviderPaymentIdText = providerPaymentIdText;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _orderRepository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<Order?> UpdateStatusByProviderIdAsync(
        long providerPaymentId,
        string status,
        string? statusDetail,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default
    )
    {
        var existing = await _orderRepository.GetByProviderPaymentIdAsync(providerPaymentId, ct);
        if (existing is null)
            return null;

        if (existing.Status == "approved")
            return existing;

        existing.Status = status;
        existing.StatusDetail = statusDetail ?? existing.StatusDetail;
        if (expiresAt is not null)
            existing.ExpiresAtUtc = expiresAt.Value.ToUniversalTime();
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _orderRepository.UpdateAsync(existing, ct);
        return existing;
    }

    public async Task<Order?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    )
    {
        return await _orderRepository.GetByProviderPaymentIdAsync(providerPaymentId, ct);
    }
}
