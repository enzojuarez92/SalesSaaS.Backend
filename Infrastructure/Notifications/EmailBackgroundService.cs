using SalesSaaS.Application.Notifications;

namespace SalesSaaS.Infrastructure.Notifications;

public sealed class EmailBackgroundService(IEmailQueue emailQueue, IServiceScopeFactory serviceScopeFactory, ILogger<EmailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in emailQueue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEmailService>().SendAsync(message, stoppingToken);
            }
            catch (Exception exception) { logger.LogError(exception, "Unable to send queued email to {Recipient}", message.To); }
        }
    }
}
