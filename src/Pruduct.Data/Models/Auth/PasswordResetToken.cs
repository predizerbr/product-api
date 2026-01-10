using Pruduct.Common.Entities;
using Pruduct.Data.Models.Users;

namespace Pruduct.Data.Models.Auth;

public class PasswordResetToken : Entity<Guid>
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public User? User { get; set; }
}
