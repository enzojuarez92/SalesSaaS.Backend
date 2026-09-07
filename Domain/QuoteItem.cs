namespace SalesSaaS.Domain;

public sealed class QuoteItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid QuoteId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }
    public Quote? Quote { get; set; }
    public Product? Product { get; set; }
}
