using SalesSaaS.Domain;

namespace SalesSaaS.Application.Security;

public interface IRefreshTokenService
{
    IssuedRefreshToken Create(Guid userId, Guid tenantId);
    string Hash(string refreshToken);
}

public sealed record IssuedRefreshToken(string Token, RefreshToken Entity);
