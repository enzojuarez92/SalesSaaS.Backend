using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record ActiveTenantDto(Guid Id, string Name, string TaxId, string? LegalName, string? TaxCondition, string? Address, string? Phone, string? LogoUrl, string PrintFormat);

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
            .Select(item => new ActiveTenantDto(item.Id, item.Name, item.TaxId, item.LegalName, item.TaxCondition, item.Address, item.Phone, item.LogoUrl, item.PrintFormat)).SingleOrDefaultAsync(cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}
