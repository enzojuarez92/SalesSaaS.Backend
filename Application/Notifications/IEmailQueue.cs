namespace SalesSaaS.Application.Notifications;

public interface IEmailQueue
{
    int PendingCount { get; }
    ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken);
    IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken);
}
