using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Pruduct.Business.Options;

namespace Pruduct.Business.Services.Mailers
{
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _opt;

        public SmtpEmailSender(IOptions<EmailOptions> opt) => _opt = opt.Value;

        public async Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken ct = default
        )
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            var security = _opt.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.SslOnConnect;
            await client.ConnectAsync(_opt.Host, _opt.Port, security, ct);
            await client.AuthenticateAsync(_opt.Username, _opt.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}
