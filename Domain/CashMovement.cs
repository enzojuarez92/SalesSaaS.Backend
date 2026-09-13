namespace SalesSaaS.Domain;
public enum PaymentMethod
{
    Cash = 1,
    CreditCard = 2,
    DebitCard = 3,
    BankTransfer = 4,
    MercadoPago = 5,
    Account = 6,
    VirtualWallet = 7,
    Other = 8
}
public sealed class CashMovement { public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid CashRegisterSessionId { get; set; } public PaymentMethod PaymentMethod { get; set; } public decimal Amount { get; set; } public bool IsIncome { get; set; } public string Description { get; set; } = string.Empty; public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow; }
