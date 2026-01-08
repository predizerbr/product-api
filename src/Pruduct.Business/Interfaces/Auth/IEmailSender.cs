namespace Pruduct.Business.Abstractions;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string token, CancellationToken ct = default);
}
