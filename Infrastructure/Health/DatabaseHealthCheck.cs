using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SalesSaaS.Infrastructure.Health;

public sealed class DatabaseHealthCheck(ApplicationDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthCheckContext, CancellationToken cancellationToken = default)
    {
        try { return await context.Database.CanConnectAsync(cancellationToken) ? HealthCheckResult.Healthy("Database connection is available.") : HealthCheckResult.Unhealthy("Database connection is unavailable."); }
        catch (Exception exception) { return HealthCheckResult.Unhealthy("Database connection failed.", exception); }
    }
}
