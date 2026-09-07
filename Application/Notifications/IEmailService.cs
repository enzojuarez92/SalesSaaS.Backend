namespace SalesSaaS.Application.Notifications;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody, IReadOnlyList<EmailAttachment>? Attachments = null);
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);
