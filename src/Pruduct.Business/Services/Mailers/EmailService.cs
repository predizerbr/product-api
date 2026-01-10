using FluentEmail.Core;
using Microsoft.Extensions.Logging;
using Pruduct.Business.Interfaces.Email;
using Pruduct.Data.Models.Emails;

namespace Pruduct.Business.Services.Mailers;

public class EmailService : IEmailService
{
    private readonly IFluentEmailFactory _factory;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IFluentEmailFactory factory, ILogger<EmailService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var email = _factory
            .Create()
            .To(message.ToEmail, message.ToName)
            .Subject(message.Subject)
            .Body(message.HtmlBody, isHtml: true);

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            email.Data.PlaintextAlternativeBody = message.TextBody;
        }

        var result = await email.SendAsync(ct);
        if (!result.Successful)
        {
            var error = string.Join("; ", result.ErrorMessages);
            _logger.LogWarning("Failed to send email to {Email}: {Error}", message.ToEmail, error);
            throw new InvalidOperationException(error);
        }
    }
}
