using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Orders;

namespace Product.Data.Repositories;

public class OrderRepository(AppDbContext db) : IOrderRepository
{
    public async Task<Order?> GetByExternalIdAsync(
        string externalOrderId,
        CancellationToken ct = default
    )
    {
        return await db.Orders.FirstOrDefaultAsync(o => o.OrderId == externalOrderId, ct);
    }

    public async Task<Order?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    )
    {
        return await db.Orders.FirstOrDefaultAsync(
            o => o.ProviderPaymentId == providerPaymentId,
            ct
        );
    }

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        db.Orders.Update(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
