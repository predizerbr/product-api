namespace Pruduct.Data.Models.Emails;

public record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string HtmlBody,
    string? TextBody = null
);
