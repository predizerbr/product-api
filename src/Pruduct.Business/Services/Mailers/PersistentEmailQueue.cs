using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pruduct.Business.Interfaces.Email;
using Pruduct.Data.Database.Contexts;
using Pruduct.Data.Models.Emails;

namespace Pruduct.Business.Services.Mailers;

public class PersistentEmailQueue : IEmailQueue
{
    private const int MaxErrorLength = 2048;
    private const int MaxAttempts = 8;

    private readonly AppDbContext _db;
    private readonly ILogger<PersistentEmailQueue> _logger;

    public PersistentEmailQueue(AppDbContext db, ILogger<PersistentEmailQueue> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task EnqueueAsync(EmailMessage message, CancellationToken ct = default)
    {
        _db.QueuedEmails.Add(
            new QueuedEmail
            {
                ToEmail = message.ToEmail,
                ToName = message.ToName,
                Subject = message.Subject,
                HtmlBody = message.HtmlBody,
                TextBody = message.TextBody,
                AttemptCount = 0,
            }
        );

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Queued email to {Email}", message.ToEmail);
    }

    public async Task<IReadOnlyCollection<QueuedEmail>> GetPendingAsync(
        int maxItems,
        CancellationToken ct = default
    )
    {
        return await _db
            .QueuedEmails.Where(x => x.SentAt == null && x.AttemptCount < MaxAttempts)
            .OrderBy(x => x.CreatedAt)
            .Take(maxItems)
            .ToListAsync(ct);
    }

    public async Task MarkSentAsync(QueuedEmail email, CancellationToken ct = default)
    {
        email.SentAt = DateTimeOffset.UtcNow;
        email.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(
        QueuedEmail email,
        string error,
        CancellationToken ct = default
    )
    {
        email.AttemptCount += 1;
        email.UpdatedAt = DateTimeOffset.UtcNow;
        email.LastError = TrimError(error);
        await _db.SaveChangesAsync(ct);
    }

    private static string? TrimError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return error;
        }

        return error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}
