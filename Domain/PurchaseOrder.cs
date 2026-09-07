namespace SalesSaaS.Domain;
public sealed class PurchaseOrder
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
