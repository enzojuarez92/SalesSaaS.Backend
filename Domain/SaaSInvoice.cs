namespace SalesSaaS.Domain;

public sealed class SaaSInvoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TenantSubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ARS";
    public SaaSInvoiceStatus Status { get; set; } = SaaSInvoiceStatus.Pending;
    public string PaymentProvider { get; set; } = string.Empty;
    public string ExternalReference { get; set; } = string.Empty;
    public string? CheckoutUrl { get; set; }
    public DateTime DueAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
