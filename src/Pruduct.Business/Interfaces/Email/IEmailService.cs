using Pruduct.Data.Models.Emails;

namespace Pruduct.Business.Interfaces.Email;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
