using Pruduct.Common.Entities;

namespace Pruduct.Data.Models.Emails;

public class QueuedEmail : Entity<Guid>
{
    public string ToEmail { get; set; } = default!;
    public string? ToName { get; set; }
    public string Subject { get; set; } = default!;
    public string HtmlBody { get; set; } = default!;
    public string? TextBody { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? LastError { get; set; }
}
