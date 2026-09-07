namespace SalesSaaS.Domain;

public sealed class SubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public string Currency { get; set; } = "ARS";
    public int MaxUsers { get; set; }
    public int MaxWarehouses { get; set; }
    public int MaxInvoicesPerMonth { get; set; }
    public bool SupportsAfip { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
