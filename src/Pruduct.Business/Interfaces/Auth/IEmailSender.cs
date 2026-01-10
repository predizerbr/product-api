namespace Pruduct.Business.Interfaces.Auth;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(
        string toEmail,
        string userName,
        string confirmUrl,
        CancellationToken ct = default
    );
    Task SendChangeEmailAsync(
        string toEmail,
        string userName,
        string confirmUrl,
        CancellationToken ct = default
    );
    Task SendForgotPasswordAsync(
        string toEmail,
        string userName,
        string resetCode,
        CancellationToken ct = default
    );
    Task SendResetPasswordConfirmationAsync(
        string toEmail,
        string userName,
        CancellationToken ct = default
    );
}
