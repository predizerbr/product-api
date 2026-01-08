namespace Pruduct.Contracts.Auth;

public class ResetPasswordRequest
{
    public string Token { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string ConfirmPassword { get; set; } = default!;
}
