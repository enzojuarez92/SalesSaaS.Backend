namespace SalesSaaS.Domain;

public sealed class SupportImpersonationLog
{
    public Guid Id { get; set; }
    public Guid SuperAdminUserId { get; set; }
    public Guid ImpersonatedUserId { get; set; }
    public Guid TenantId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
}
