namespace SalesSaaS.Domain;

public sealed class CustomerAccountEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? InvoiceId { get; set; }
    public CustomerAccountEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public Customer? Customer { get; set; }
    public Invoice? Invoice { get; set; }
}
