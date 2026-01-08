namespace Pruduct.Contracts.Users;

public class UserSessionResponse
{
    public Guid Id { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsActive { get; set; }
}
