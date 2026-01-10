namespace Pruduct.Contracts.Auth;

public class ResetPasswordRequest
{
    public string Email { get; set; } = default!;
    public string ResetCode { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string ConfirmPassword { get; set; } = default!;
}
