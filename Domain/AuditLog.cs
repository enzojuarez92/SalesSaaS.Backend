namespace SalesSaaS.Domain;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    // Acciones de configuración global no pertenecen a una sucursal.
    public Guid? WarehouseId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public AuditAction Action { get; set; }
    public string ChangesJson { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
