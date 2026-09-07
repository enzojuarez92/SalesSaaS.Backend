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
    string Role)
{
    public static AuthResponse From(
        AuthToken accessToken,
        IssuedRefreshToken refreshToken,
        User user,
        TenantMembership membership) =>
        new(
            accessToken.AccessToken,
            accessToken.ExpiresAtUtc,
            refreshToken.Token,
            refreshToken.Entity.ExpiresAtUtc,
            user.Id,
            user.Email,
            membership.TenantId,
            membership.Role);
}
