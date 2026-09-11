namespace SalesSaaS.Domain;

public sealed class StockMovement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? UserId { get; set; }
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public string? Reason { get; set; }
    public string? Reference { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
}
