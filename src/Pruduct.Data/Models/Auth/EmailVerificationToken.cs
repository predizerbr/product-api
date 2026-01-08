using Pruduct.Common.Entities;

namespace Pruduct.Data.Models;

public class EmailVerificationToken : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public User? User { get; set; }
}
