using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pruduct.Business.Interfaces.Email;
using Pruduct.Data.Models.Emails;

namespace Pruduct.Business.BackgroundServices;

public class PersistentEmailBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PersistentEmailBackgroundService> _logger;

    public PersistentEmailBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PersistentEmailBackgroundService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email background service failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IEmailQueue>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var pending = await queue.GetPendingAsync(20, ct);
        foreach (var email in pending)
        {
            try
            {
                await sender.SendAsync(
                    new EmailMessage(
                        email.ToEmail,
                        email.ToName,
                        email.Subject,
                        email.HtmlBody,
                        email.TextBody
                    ),
                    ct
                );

                await queue.MarkSentAsync(email, ct);
            }
            catch (Exception ex)
            {
                await queue.MarkFailedAsync(email, ex.ToString(), ct);
            }
        }
    }
}
