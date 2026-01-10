namespace Pruduct.Business.Options;

public class IdentityTokenOptions
{
    public const string SectionName = "IdentityTokens";
    public int BearerTokenExpirationInMinutes { get; set; } = 60;
    public int RefreshTokenExpirationInDays { get; set; } = 30;
    public int PasswordResetTokenExpirationInMinutes { get; set; } = 60;
    public int EmailConfirmationTokenExpirationInHours { get; set; } = 48;
}
