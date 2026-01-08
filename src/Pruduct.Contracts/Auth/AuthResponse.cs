using Pruduct.Contracts.Users;

namespace Pruduct.Contracts.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public UserView User { get; set; } = default!;
}
