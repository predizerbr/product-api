using Product.Data.Models.Orders;

namespace Product.Data.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByExternalIdAsync(string externalOrderId, CancellationToken ct = default);
    Task<Order?> GetByProviderPaymentIdAsync(long providerPaymentId, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
