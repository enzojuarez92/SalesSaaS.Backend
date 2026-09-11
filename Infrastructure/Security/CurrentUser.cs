using System.Security.Claims;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Infrastructure.Security;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated is true;

    public Guid? UserId => GetGuidValue(ClaimTypes.NameIdentifier);

    public Guid? TenantId => GetGuidValue("tenant_id");
    public Guid? WarehouseId => httpContextAccessor.HttpContext?.Items["WarehouseId"] as Guid?;

    private Guid? GetGuidValue(string claimType)
    {
        var rawValue = Principal?.FindFirstValue(claimType);
        return Guid.TryParse(rawValue, out var value) ? value : null;
    }
}
