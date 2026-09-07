namespace SalesSaaS.Domain;

public sealed class Quote
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime ExpiresAtUtc { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Customer? Customer { get; set; }
    public ICollection<QuoteItem> Items { get; set; } = new List<QuoteItem>();
}
