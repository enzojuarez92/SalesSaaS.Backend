namespace SalesSaaS.Domain;

public sealed class TenantFiscalProfile
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string IssuerTaxId { get; set; } = string.Empty;
    public string CertificateContentEncrypted { get; set; } = string.Empty;
    public string? PrivateKeyContentEncrypted { get; set; }
    public string? CertificatePassphraseEncrypted { get; set; }
    public string CertificateAlias { get; set; } = string.Empty;
    public bool IsPfxCertificate { get; set; } = true;
    public AfipEnvironment Environment { get; set; } = AfipEnvironment.Homologation;
    public int SalesPoint { get; set; }
    public AfipConcept DefaultConcept { get; set; } = AfipConcept.Products;
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
