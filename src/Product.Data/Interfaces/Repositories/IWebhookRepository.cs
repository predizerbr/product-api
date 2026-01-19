using Product.Data.Models.Webhooks;

namespace Product.Data.Interfaces.Repositories;

public interface IWebhookRepository
{
    Task AddAsync(MPWebhookEvent webhookEvent, CancellationToken ct = default);
    Task<MPWebhookEvent?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    );
    Task<MPWebhookEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(MPWebhookEvent webhookEvent, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
