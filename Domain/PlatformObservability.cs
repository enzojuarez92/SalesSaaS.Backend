namespace SalesSaaS.Domain;

public sealed class ApiEndpointMetric
{
    public Guid Id { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long RequestCount { get; set; }
    public long FailedRequestCount { get; set; }
    public long TotalDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public DateTime LastOccurredAtUtc { get; set; }
}

public sealed class PlatformErrorLog
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public int StatusCode { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string ErrorType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
}
