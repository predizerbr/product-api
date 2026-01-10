using Pruduct.Data.Models.Emails;

namespace Pruduct.Business.Interfaces.Email;

public interface IEmailQueue
{
    Task EnqueueAsync(EmailMessage message, CancellationToken ct = default);
    Task<IReadOnlyCollection<QueuedEmail>> GetPendingAsync(
        int maxItems,
        CancellationToken ct = default
    );
    Task MarkSentAsync(QueuedEmail email, CancellationToken ct = default);
    Task MarkFailedAsync(QueuedEmail email, string error, CancellationToken ct = default);
}
