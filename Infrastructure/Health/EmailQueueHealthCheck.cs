using Microsoft.Extensions.Diagnostics.HealthChecks;
using SalesSaaS.Application.Notifications;

namespace SalesSaaS.Infrastructure.Health;

public sealed class EmailQueueHealthCheck(IEmailQueue emailQueue) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(emailQueue.PendingCount < 1000 ? HealthCheckResult.Healthy($"Queued emails: {emailQueue.PendingCount}.") : HealthCheckResult.Degraded($"Queued emails exceeded the safe threshold: {emailQueue.PendingCount}."));
}
