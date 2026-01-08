using Pruduct.Common.Entities;

namespace Pruduct.Data.Models;

public class AuditLog : Entity<Guid>
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string Entity { get; set; } = default!;
    public Guid? EntityId { get; set; }
    public string? MetaJson { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
