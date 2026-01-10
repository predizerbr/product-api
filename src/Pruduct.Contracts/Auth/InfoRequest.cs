namespace Pruduct.Contracts.Auth;

public class InfoRequest
{
    public string? NewEmail { get; set; }
    public string? OldPassword { get; set; }
    public string? NewPassword { get; set; }
}
