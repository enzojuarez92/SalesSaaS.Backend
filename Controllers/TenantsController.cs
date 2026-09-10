using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record ActiveTenantDto(Guid Id, string Name);

[ApiController]
[Route("api/tenants")]
[Authorize]
public sealed class TenantsController(ApplicationDbContext context, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<ActiveTenantDto>> Current(CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue) return Unauthorized();
        var tenant = await context.Tenants.AsNoTracking().Where(item => item.Id == currentUser.TenantId.Value)
            .Select(item => new ActiveTenantDto(item.Id, item.Name)).SingleOrDefaultAsync(cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}
