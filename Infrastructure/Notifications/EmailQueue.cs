using System.Threading.Channels;
using SalesSaaS.Application.Notifications;

namespace SalesSaaS.Infrastructure.Notifications;

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();
    public ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken) => _channel.Writer.WriteAsync(message, cancellationToken);
    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}
