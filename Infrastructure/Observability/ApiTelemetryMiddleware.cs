using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Observability;

public sealed class ApiTelemetryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, ApplicationDbContext context, ILogger<ApiTelemetryMiddleware> logger)
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
        {
            await next(httpContext);
            return;
        }

        var timer = Stopwatch.StartNew();
        var statusCode = StatusCodes.Status200OK;
        try
        {
            await next(httpContext);
            statusCode = httpContext.Response.StatusCode;
        }
        catch
        {
            statusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            timer.Stop();
            try
            {
                await RecordAsync(context, httpContext.Request.Method, NormalizePath(httpContext.Request.Path), statusCode, Math.Max(1, timer.ElapsedMilliseconds), httpContext.RequestAborted);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not persist API telemetry for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            }
        }
    }

    private static async Task RecordAsync(ApplicationDbContext context, string method, string path, int statusCode, long elapsedMilliseconds, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var period = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var failure = statusCode >= StatusCodes.Status400BadRequest ? 1L : 0L;
        var updated = await context.ApiEndpointMetrics
            .Where(item => item.PeriodStartUtc == period && item.Method == method && item.Path == path)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.RequestCount, item => item.RequestCount + 1)
                .SetProperty(item => item.FailedRequestCount, item => item.FailedRequestCount + failure)
                .SetProperty(item => item.TotalDurationMs, item => item.TotalDurationMs + elapsedMilliseconds)
                .SetProperty(item => item.MaxDurationMs, item => item.MaxDurationMs < elapsedMilliseconds ? elapsedMilliseconds : item.MaxDurationMs)
                .SetProperty(item => item.LastOccurredAtUtc, now), cancellationToken);

        if (updated > 0) return;

        context.ApiEndpointMetrics.Add(new ApiEndpointMetric
        {
            Id = Guid.NewGuid(),
            PeriodStartUtc = period,
            Method = method,
            Path = path,
            RequestCount = 1,
            FailedRequestCount = failure,
            TotalDurationMs = elapsedMilliseconds,
            MaxDurationMs = elapsedMilliseconds,
            LastOccurredAtUtc = now
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            context.ChangeTracker.Clear();
            await context.ApiEndpointMetrics
                .Where(item => item.PeriodStartUtc == period && item.Method == method && item.Path == path)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.RequestCount, item => item.RequestCount + 1)
                    .SetProperty(item => item.FailedRequestCount, item => item.FailedRequestCount + failure)
                    .SetProperty(item => item.TotalDurationMs, item => item.TotalDurationMs + elapsedMilliseconds)
                    .SetProperty(item => item.MaxDurationMs, item => item.MaxDurationMs < elapsedMilliseconds ? elapsedMilliseconds : item.MaxDurationMs)
                    .SetProperty(item => item.LastOccurredAtUtc, now), cancellationToken);
        }
    }

    private static string NormalizePath(PathString path)
    {
        var segments = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => Guid.TryParse(segment, out _) || long.TryParse(segment, out _) ? ":id" : segment);
        return "/" + string.Join('/', segments);
    }
}
