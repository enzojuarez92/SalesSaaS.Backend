namespace SalesSaaS.Domain;

public sealed class TenantMercadoPagoSettings
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? PublicKey { get; set; }
    public string? AccessTokenEncrypted { get; set; }
    public string? WebhookSecretEncrypted { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
