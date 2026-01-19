using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Webhooks;

namespace Product.Data.Repositories;

public class WebhookRepository(AppDbContext db) : IWebhookRepository
{
    public async Task AddAsync(MPWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        db.MPWebhookEvent.Add(webhookEvent);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MPWebhookEvent?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    )
    {
        return await db.MPWebhookEvent.FirstOrDefaultAsync(
            w => w.ProviderPaymentId == providerPaymentId,
            ct
        );
    }

    public async Task<MPWebhookEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.MPWebhookEvent.FindAsync(new object[] { id }, ct);
    }

    public async Task UpdateAsync(MPWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        db.MPWebhookEvent.Update(webhookEvent);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
