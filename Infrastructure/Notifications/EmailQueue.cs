using System.Threading.Channels;
using SalesSaaS.Application.Notifications;

namespace SalesSaaS.Infrastructure.Notifications;

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>();
    private int _pendingCount;
    public int PendingCount => Volatile.Read(ref _pendingCount);
    public async ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _pendingCount);
        try { await _channel.Writer.WriteAsync(message, cancellationToken); }
        catch { Interlocked.Decrement(ref _pendingCount); throw; }
    }
    public async IAsyncEnumerable<EmailMessage> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _pendingCount);
            yield return message;
        }
    }
}
