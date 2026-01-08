namespace Pruduct.Contracts.Users;

public class UserView
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
    public UserPersonalDataView? PersonalData { get; set; }
}
