using SalesSaaS.Domain;

namespace SalesSaaS.Application.Security;

public interface IJwtTokenService
{
    AuthToken Create(User user, TenantMembership membership, Guid? impersonatorUserId = null, Guid? supportImpersonationLogId = null);
}

public sealed record AuthToken(string AccessToken, DateTime ExpiresAtUtc);
