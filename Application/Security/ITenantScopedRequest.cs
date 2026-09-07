namespace SalesSaaS.Application.Security;

public interface ITenantScopedRequest
{
    Guid TenantId { get; }
}
