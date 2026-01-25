using Microsoft.EntityFrameworkCore;
using Product.Common.Utilities;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Webhooks;

namespace Product.Data.Repositories;

public class MercadoPagoRepository : IMercadoPagoRepository
{
    private readonly AppDbContext _db;

    public MercadoPagoRepository(AppDbContext db) => _db = db;

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
            SignatureHeader = HeaderUtils.ExtractSignatureFromHeaders(headers),
            ReceivedAt = DateTimeOffset.UtcNow,
            AttemptCount = 1,
        };

        _db.MPWebhookEvent.Add(ev);
        await _db.SaveChangesAsync(ct);
        return ev;
    }

    public async Task<MPWebhookEvent?> GetByProviderPaymentIdAsync(
        long providerPaymentId,
        CancellationToken ct = default
    )
    {
        return await _db.MPWebhookEvent.FirstOrDefaultAsync(
            w => w.ProviderPaymentId == providerPaymentId,
            ct
        );
    }

    public async Task<MPWebhookEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.MPWebhookEvent.FindAsync([id], ct);
    }

    public async Task MarkProcessedAsync(
        Guid id,
        bool processed,
        string? result,
        string? orderId = null,
        int? responseStatusCode = null,
        int? processingDurationMs = null,
        CancellationToken ct = default
    )
    {
        var ev = await GetByIdAsync(id, ct);
        if (ev is null)
            return;
        ev.Processed = processed;
        ev.ProcessedAt = DateTimeOffset.UtcNow;
        ev.AttemptCount += 1;
        ev.ProcessingResult = result;
        if (responseStatusCode.HasValue)
            ev.ResponseStatusCode = responseStatusCode.Value;
        if (processingDurationMs.HasValue)
            ev.ProcessingDurationMs = processingDurationMs.Value;
        if (!string.IsNullOrWhiteSpace(orderId))
            ev.OrderId = orderId;

        _db.MPWebhookEvent.Update(ev);
        await _db.SaveChangesAsync(ct);
    }
}
