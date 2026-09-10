using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Features.Afip;
using SalesSaaS.Features.TenantMemberships.Commands;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record BusinessSettingsDto(Guid TenantId, string Name, string? LegalName, string TaxId, string? TaxCondition, string? Address, string? Phone, string? LogoUrl);
public sealed record UpdateBusinessSettingsRequest(Guid TenantId, string Name, string? LegalName, string TaxId, string? TaxCondition, string? Address, string? Phone, string? LogoUrl);
public sealed record TenantUserDto(Guid Id, string FirstName, string LastName, string Email, string Role, bool IsActive);
public sealed record UpdateTenantUserRequest(Guid Id, Guid TenantId, string FirstName, string LastName, string Role);
public sealed record ToggleTenantUserRequest(Guid TenantId, bool IsActive);

[ApiController]
[Route("api/settings")]
[Authorize(Roles = Roles.Administration)]
public sealed class SettingsController(ApplicationDbContext context, ISender sender) : ControllerBase
{
    [HttpGet("business")]
    public async Task<BusinessSettingsDto> Business([FromQuery] Guid tenantId) { var t = await context.Tenants.SingleAsync(x => x.Id == tenantId); return new(t.Id,t.Name,t.LegalName,t.TaxId,t.TaxCondition,t.Address,t.Phone,t.LogoUrl); }
    [HttpPut("business")]
    public async Task<BusinessSettingsDto> UpdateBusiness(UpdateBusinessSettingsRequest request) { var t=await context.Tenants.SingleAsync(x=>x.Id==request.TenantId); t.Name=request.Name.Trim(); t.LegalName=request.LegalName?.Trim(); t.TaxId=request.TaxId.Trim(); t.TaxCondition=request.TaxCondition?.Trim(); t.Address=request.Address?.Trim(); t.Phone=request.Phone?.Trim(); t.LogoUrl=request.LogoUrl?.Trim(); await context.SaveChangesAsync(); return new(t.Id,t.Name,t.LegalName,t.TaxId,t.TaxCondition,t.Address,t.Phone,t.LogoUrl); }
    [HttpPost("afip-cert")]
    public async Task<IActionResult> AfipCert(ConfigureTenantFiscalProfileCommand command) => Ok(new { id = await sender.Send(command) });
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Administration)]
public sealed class UsersController(ApplicationDbContext context, ISender sender) : ControllerBase
{
    [HttpGet] public async Task<IReadOnlyList<TenantUserDto>> List([FromQuery] Guid tenantId) => await context.TenantMemberships.AsNoTracking().Include(x=>x.User).Where(x=>x.TenantId==tenantId).Select(x=>new TenantUserDto(x.UserId,x.User!.FirstName,x.User.LastName,x.User.Email,x.Role,x.IsActive && x.User.IsActive)).ToListAsync();
    [HttpPost] public async Task<IActionResult> Create(CreateTenantMemberCommand command) => Created("/api/users", new { id=await sender.Send(command) });
    [HttpPut("{id:guid}")] public async Task<TenantUserDto> Update(Guid id, UpdateTenantUserRequest request) { if(id!=request.Id) throw new InvalidOperationException("El usuario no coincide."); var m=await context.TenantMemberships.Include(x=>x.User).SingleAsync(x=>x.UserId==id&&x.TenantId==request.TenantId); m.User!.FirstName=request.FirstName.Trim();m.User.LastName=request.LastName.Trim();m.Role=request.Role;await context.SaveChangesAsync();return new(m.UserId,m.User.FirstName,m.User.LastName,m.User.Email,m.Role,m.IsActive&&m.User.IsActive); }
    [HttpPut("{id:guid}/toggle-status")] public async Task<IActionResult> Toggle(Guid id, ToggleTenantUserRequest request) { var m=await context.TenantMemberships.SingleAsync(x=>x.UserId==id&&x.TenantId==request.TenantId);m.IsActive=request.IsActive;await context.SaveChangesAsync();return NoContent(); }
}
