namespace SalesSaaS.Infrastructure.Afip;

public interface IFiscalProfileSecretProtector
{
    string Protect(string value);
    string Unprotect(string value);
}
