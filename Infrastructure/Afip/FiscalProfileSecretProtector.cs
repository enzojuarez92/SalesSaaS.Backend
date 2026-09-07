using Microsoft.AspNetCore.DataProtection;

namespace SalesSaaS.Infrastructure.Afip;

public sealed class FiscalProfileSecretProtector(IDataProtectionProvider dataProtectionProvider) : IFiscalProfileSecretProtector
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("SalesSaaS.Afip.TenantFiscalProfile.v1");

    public string Protect(string value) => _protector.Protect(value);
    public string Unprotect(string value) => _protector.Unprotect(value);
}
