using Pruduct.Common.Entities;

namespace Pruduct.Data.Models;

public class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? DeviceInfo { get; set; }
}
