using Microsoft.AspNetCore.Identity;

namespace Pruduct.Data.Models.Users;

public class User : IdentityUser<Guid>
{
    public string Name { get; set; } = default!;
    public string NormalizedName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public UserPersonalData? PersonalData { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
