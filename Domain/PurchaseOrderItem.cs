namespace SalesSaaS.Domain;

public sealed class PurchaseOrderItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalAmount { get; set; }
}
