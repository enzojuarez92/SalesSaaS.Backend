namespace SalesSaaS.Application.Notifications;

public interface IEmailQueue
{
    ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken);
    IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken);
}
