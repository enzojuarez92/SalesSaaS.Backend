using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Security;

public sealed class RefreshTokenService(IOptions<RefreshTokenOptions> options) : IRefreshTokenService
{
    private readonly RefreshTokenOptions _options = options.Value;

    public IssuedRefreshToken Create(Guid userId, Guid tenantId, Guid? impersonatorUserId = null, Guid? supportImpersonationLogId = null)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            TokenHash = Hash(token),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_options.ExpirationDays),
            ImpersonatorUserId = impersonatorUserId,
            SupportImpersonationLogId = supportImpersonationLogId
        };

        return new IssuedRefreshToken(token, entity);
    }

    public string Hash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
