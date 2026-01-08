namespace Pruduct.Contracts.Auth;

public class ForgotPasswordResponse
{
    public string? ResetToken { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
