using Pruduct.Common.Entities;

namespace Pruduct.Data.Models;

public class User : Entity<Guid>
{
    public string Email { get; set; } = default!;
    public string NormalizedEmail { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string NormalizedUsername { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string NormalizedName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string PasswordHash { get; set; } = default!;
    public string Status { get; set; } = "ACTIVE";
    public UserPersonalData? PersonalData { get; set; }
}
