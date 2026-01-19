using Microsoft.Extensions.Logging;
using Product.Business.Interfaces.Email;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Emails;

namespace Product.Business.Services.Mailers;

public class PersistentEmailQueue : IEmailQueue
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly ILogger<PersistentEmailQueue> _logger;

    public PersistentEmailQueue(
        IEmailQueueRepository emailQueueRepository,
        ILogger<PersistentEmailQueue> logger
    )
    {
        _emailQueueRepository = emailQueueRepository;
        _logger = logger;
    }

    public async Task EnqueueAsync(EmailMessage message, CancellationToken ct = default)
    {
        await _emailQueueRepository.AddAsync(
            new QueuedEmail
            {
                ToEmail = message.ToEmail,
                ToName = message.ToName,
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                TextBody = message.TextBody,
                AttemptCount = 0,
            },
            ct
        );
        _logger.LogInformation("Queued email to {Email}", message.ToEmail);
    }

    public async Task<IReadOnlyCollection<QueuedEmail>> GetPendingAsync(
        int maxItems,
        CancellationToken ct = default
    )
    {
        return await _emailQueueRepository.GetPendingAsync(maxItems, ct);
    }

    public async Task MarkSentAsync(QueuedEmail email, CancellationToken ct = default)
    {
        await _emailQueueRepository.MarkSentAsync(email, ct);
    }

    public async Task MarkFailedAsync(
        QueuedEmail email,
        string error,
        CancellationToken ct = default
    )
    {
        await _emailQueueRepository.MarkFailedAsync(email, error, ct);
    }
}
