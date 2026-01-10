namespace Pruduct.Contracts.Auth;

public class TwoFactorResponse
{
    public string? SharedKey { get; set; }
    public int RecoveryCodesLeft { get; set; }
    public string[] RecoveryCodes { get; set; } = Array.Empty<string>();
    public bool IsTwoFactorEnabled { get; set; }
    public bool IsMachineRemembered { get; set; }
}
