using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pruduct.Business.Abstractions;
using Pruduct.Business.Options;

namespace Pruduct.Business.Services;

public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly EmailOptions _options;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IOptions<EmailOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task SendEmailVerificationAsync(
        string toEmail,
        string token,
        CancellationToken ct = default
    )
    {
        var link = string.IsNullOrWhiteSpace(_options.VerifyEmailUrlBase)
            ? string.Empty
            : $"{_options.VerifyEmailUrlBase}?token={Uri.EscapeDataString(token)}";

        _logger.LogInformation(
            "Email verification token for {Email}: {Token} {Link}",
            toEmail,
            token,
            link
        );

        return Task.CompletedTask;
    }
}
