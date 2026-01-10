namespace Pruduct.Contracts.Auth;

public class VerifyResetCodeRequest
{
    public string Email { get; set; } = default!;
    public string ResetCode { get; set; } = default!;
}
