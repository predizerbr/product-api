using Microsoft.Extensions.Options;
using Pruduct.Business.Interfaces.Email;
using Pruduct.Business.Options;
using Pruduct.Data.Models.Emails;

namespace Pruduct.Business.Services.Mailers;

public class QueuedEmailSender : IEmailSender, Pruduct.Business.Interfaces.Auth.IEmailSender
{
    private readonly IEmailQueue _queue;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly EmailOptions _options;

    public QueuedEmailSender(
        IEmailQueue queue,
        IEmailTemplateRenderer renderer,
        IOptions<EmailOptions> options
    )
    {
        _queue = queue;
        _renderer = renderer;
        _options = options.Value;
    }

    // Implementation for generic mailer interface used elsewhere in the app
    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default
    )
    {
        await _queue.EnqueueAsync(new EmailMessage(toEmail, toEmail, subject, htmlBody), ct);
    }

    public async Task SendEmailVerificationAsync(
        string toEmail,
        string userName,
        string confirmUrl,
        CancellationToken ct = default
    )
    {
        var model = new ConfirmationEmailModel { UserName = userName, ConfirmUrl = confirmUrl };
        var html = await _renderer.RenderAsync("ConfirmationEmail", model, ct);
        await _queue.EnqueueAsync(
            new EmailMessage(toEmail, userName, $"{_options.FromName} - confirm your email", html),
            ct
        );
    }

    public async Task SendChangeEmailAsync(
        string toEmail,
        string userName,
        string confirmUrl,
        CancellationToken ct = default
    )
    {
        var model = new ChangeEmailModel { UserName = userName, ConfirmUrl = confirmUrl };
        var html = await _renderer.RenderAsync("ChangeEmail", model, ct);
        await _queue.EnqueueAsync(
            new EmailMessage(
                toEmail,
                userName,
                $"{_options.FromName} - confirm your email change",
                html
            ),
            ct
        );
    }

    public async Task SendForgotPasswordAsync(
        string toEmail,
        string userName,
        string resetCode,
        CancellationToken ct = default
    )
    {
        var model = new ForgotPasswordEmailModel { UserName = userName, ResetCode = resetCode };
        var html = await _renderer.RenderAsync("ForgotPassword", model, ct);
        await _queue.EnqueueAsync(
            new EmailMessage(toEmail, userName, $"{_options.FromName} - password reset code", html),
            ct
        );
    }

    public async Task SendResetPasswordConfirmationAsync(
        string toEmail,
        string userName,
        CancellationToken ct = default
    )
    {
        var model = new ResetPasswordConfirmationModel { UserName = userName };
        var html = await _renderer.RenderAsync("ConfirmResetPassword", model, ct);
        await _queue.EnqueueAsync(
            new EmailMessage(toEmail, userName, $"{_options.FromName} - password updated", html),
            ct
        );
    }
}
