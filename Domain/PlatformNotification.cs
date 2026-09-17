namespace SalesSaaS.Domain;

public sealed class PlatformNotification
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public Guid? TargetTenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PlatformNotificationRead
{
    public Guid Id { get; set; }
    public Guid PlatformNotificationId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;
}
