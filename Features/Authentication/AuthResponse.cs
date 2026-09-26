using SalesSaaS.Application.Security;
using SalesSaaS.Domain;

namespace SalesSaaS.Features.Authentication;

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string Email,
    Guid TenantId,
    string Role,
    Guid? ImpersonatorUserId,
    Guid? SupportImpersonationLogId)
{
    public static AuthResponse From(
        AuthToken accessToken,
        IssuedRefreshToken refreshToken,
        User user,
        TenantMembership membership,
        Guid? impersonatorUserId = null,
        Guid? supportImpersonationLogId = null) =>
        new(
            accessToken.AccessToken,
            accessToken.ExpiresAtUtc,
            refreshToken.Token,
            refreshToken.Entity.ExpiresAtUtc,
            user.Id,
            user.Email,
            membership.TenantId,
            membership.Role,
            impersonatorUserId,
            supportImpersonationLogId);
}
