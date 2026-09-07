namespace SalesSaaS.Domain;

public sealed class Invoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Status { get; set; } = "Issued";
    public decimal TotalAmount { get; set; }
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueAtUtc { get; set; }
    public Order? Order { get; set; }
    public Customer? Customer { get; set; }
}
