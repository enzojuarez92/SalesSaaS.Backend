namespace SalesSaaS.Domain;
public sealed class PurchaseInvoice { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid PurchaseOrderId { get; set; } public Guid SupplierId { get; set; } public string Number { get; set; } = string.Empty; public decimal TotalAmount { get; set; } public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow; }
