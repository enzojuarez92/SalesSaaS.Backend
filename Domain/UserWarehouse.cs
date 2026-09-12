namespace SalesSaaS.Domain;

/// <summary>Explicit branch access for operational users.</summary>
public sealed class UserWarehouse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
    public Tenant? Tenant { get; set; }
    public Warehouse? Warehouse { get; set; }
}
