using Microsoft.EntityFrameworkCore;
using Product.Data.Database.Contexts;
using Product.Data.Interfaces.Repositories;
using Product.Data.Models.Emails;

namespace Product.Data.Repositories;

public class EmailQueueRepository(AppDbContext db) : IEmailQueueRepository
{
    private const int MaxErrorLength = 2048;
    private const int MaxAttempts = 8;

    public async Task AddAsync(QueuedEmail email, CancellationToken ct = default)
    {
        db.QueuedEmails.Add(email);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyCollection<QueuedEmail>> GetPendingAsync(
        int maxItems,
        CancellationToken ct = default
    )
    {
        return await db
            .QueuedEmails.Where(x => x.SentAt == null && x.AttemptCount < MaxAttempts)
            .OrderBy(x => x.CreatedAt)
            .Take(maxItems)
            .ToListAsync(ct);
    }

    public async Task MarkSentAsync(QueuedEmail email, CancellationToken ct = default)
    {
        email.SentAt = DateTimeOffset.UtcNow;
        email.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(
        QueuedEmail email,
        string? error,
        CancellationToken ct = default
    )
    {
        email.AttemptCount += 1;
        email.UpdatedAt = DateTimeOffset.UtcNow;
        email.LastError = TrimError(error);
        await db.SaveChangesAsync(ct);
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
