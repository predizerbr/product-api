using Product.Business.Interfaces.Payments;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Webhooks;

namespace Product.Business.Services.Payments;

public class WebhookService : IWebhookService
{
    private readonly IWebhookRepository _webhookRepository;

    public WebhookService(IWebhookRepository webhookRepository)
    {
        _webhookRepository = webhookRepository;
    }

    public async Task<MPWebhookEvent> SaveAsync(
        string provider,
        string eventType,
        long? providerPaymentId,
        string? orderId,
        string payload,
        string? headers,
        CancellationToken ct = default
    )
    {
        var ev = new MPWebhookEvent
        {
            Provider = provider,
            EventType = eventType,
            ProviderPaymentId = providerPaymentId,
            OrderId = orderId,
            Payload = payload,
            Headers = headers,
            ReceivedAt = DateTimeOffset.UtcNow,
            AttemptCount = 1,
        };

        await _webhookRepository.AddAsync(ev, ct);
        return ev;
    }

    public async Task<MPWebhookEvent?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    )
    {
        return await _webhookRepository.GetByProviderPaymentIdAsync(providerPaymentId, ct);
    }

    public async Task MarkProcessedAsync(
        Guid id,
        bool processed,
        string? result,
        string? orderId = null,
        CancellationToken ct = default
    )
    {
        var ev = await _webhookRepository.GetByIdAsync(id, ct);
        if (ev is null)
            return;
        ev.Processed = processed;
        ev.ProcessedAt = DateTimeOffset.UtcNow;
        ev.AttemptCount += 1;
        ev.ProcessingResult = result;
        if (!string.IsNullOrWhiteSpace(orderId))
            ev.OrderId = orderId;
        await _webhookRepository.UpdateAsync(ev, ct);
    }
}
