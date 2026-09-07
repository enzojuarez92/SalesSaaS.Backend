using SalesSaaS.Domain;

namespace SalesSaaS.Application.Security;

public interface IJwtTokenService
{
    AuthToken Create(User user, TenantMembership membership);
}

public sealed record AuthToken(string AccessToken, DateTime ExpiresAtUtc);
