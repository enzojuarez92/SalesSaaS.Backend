using SalesSaaS.Domain;

namespace SalesSaaS.Application.Security;

public interface IRefreshTokenService
{
    IssuedRefreshToken Create(Guid userId, Guid tenantId, Guid? impersonatorUserId = null, Guid? supportImpersonationLogId = null);
    string Hash(string refreshToken);
}

public sealed record IssuedRefreshToken(string Token, RefreshToken Entity);
