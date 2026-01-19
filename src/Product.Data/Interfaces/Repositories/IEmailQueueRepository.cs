using Product.Data.Models.Emails;

namespace Product.Data.Interfaces.Repositories;

public interface IEmailQueueRepository
{
    Task AddAsync(QueuedEmail email, CancellationToken ct = default);
    Task<IReadOnlyCollection<QueuedEmail>> GetPendingAsync(
        int maxItems,
        CancellationToken ct = default
    );
    Task MarkSentAsync(QueuedEmail email, CancellationToken ct = default);
    Task MarkFailedAsync(QueuedEmail email, string? error, CancellationToken ct = default);
}
