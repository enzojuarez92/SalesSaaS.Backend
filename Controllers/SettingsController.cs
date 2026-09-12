using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Features.Afip;
using SalesSaaS.Features.TenantMemberships.Commands;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record BusinessSettingsDto(Guid TenantId, [Required, StringLength(150)] string Name, [StringLength(150)] string? LegalName, [Required, RegularExpression(@"^[0-9]{11}$")] string TaxId, [StringLength(80)] string? TaxCondition, [StringLength(300)] string? Address, [StringLength(30)] string? Phone, [StringLength(2000), Url] string? LogoUrl);
public sealed record UpdateBusinessSettingsRequest(Guid TenantId, [Required, StringLength(150)] string Name, [StringLength(150)] string? LegalName, [Required, RegularExpression(@"^[0-9]{11}$")] string TaxId, [StringLength(80)] string? TaxCondition, [StringLength(300)] string? Address, [StringLength(30)] string? Phone, [StringLength(2000), Url] string? LogoUrl);
public sealed record TenantUserDto(Guid Id, string FirstName, string LastName, string Email, string Role, bool IsActive, IReadOnlyList<Guid> WarehouseIds);
public sealed record UpdateTenantUserRequest(Guid Id, Guid TenantId, [Required, StringLength(100)] string FirstName, [Required, StringLength(100)] string LastName, [Required, RegularExpression("^(Owner|Admin|Seller|Warehouse)$")] string Role, IReadOnlyList<Guid>? WarehouseIds = null);
public sealed record ToggleTenantUserRequest(Guid TenantId, bool IsActive);
public sealed record UpdateUserWarehousesRequest(Guid TenantId, IReadOnlyList<Guid> WarehouseIds);

[ApiController]
[Route("api/settings")]
[Authorize(Roles = Roles.Administration)]
public sealed class SettingsController(ApplicationDbContext context, ISender sender) : ControllerBase
{
    [HttpGet("business")]
    public async Task<BusinessSettingsDto> Business([FromQuery] Guid tenantId) { var t = await context.Tenants.SingleAsync(x => x.Id == tenantId); return new(t.Id,t.Name,t.LegalName,t.TaxId,t.TaxCondition,t.Address,t.Phone,t.LogoUrl); }
    [HttpPut("business")]
    public async Task<BusinessSettingsDto> UpdateBusiness(UpdateBusinessSettingsRequest request) { if (!SalesSaaS.Application.Validation.ArgentineTaxId.IsValid(request.TaxId)) throw new InvalidOperationException("El CUIT no es válido."); var t=await context.Tenants.SingleAsync(x=>x.Id==request.TenantId); t.Name=request.Name.Trim(); t.LegalName=request.LegalName?.Trim(); t.TaxId=request.TaxId.Trim(); t.TaxCondition=request.TaxCondition?.Trim(); t.Address=request.Address?.Trim(); t.Phone=request.Phone?.Trim(); t.LogoUrl=request.LogoUrl?.Trim(); await context.SaveChangesAsync(); return new(t.Id,t.Name,t.LegalName,t.TaxId,t.TaxCondition,t.Address,t.Phone,t.LogoUrl); }
    [HttpPost("afip-cert")]
    public async Task<IActionResult> AfipCert(ConfigureTenantFiscalProfileCommand command) => Ok(new { id = await sender.Send(command) });
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Administration)]
public sealed class UsersController(ApplicationDbContext context, ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<TenantUserDto>> List([FromQuery] Guid tenantId)
    {
        var members = await context.TenantMemberships.AsNoTracking().Include(x => x.User).Where(x => x.TenantId == tenantId).ToListAsync();
        var assignments = await context.UserWarehouses.AsNoTracking().Where(x => x.TenantId == tenantId).GroupBy(x => x.UserId).ToDictionaryAsync(x => x.Key, x => (IReadOnlyList<Guid>)x.Select(y => y.WarehouseId).ToList());
        return members.Select(x => new TenantUserDto(x.UserId, x.User!.FirstName, x.User.LastName, x.User.Email, x.Role, x.IsActive && x.User.IsActive, assignments.GetValueOrDefault(x.UserId, []))).ToList();
    }
    [HttpPost] public async Task<IActionResult> Create(CreateTenantMemberCommand command) => Created("/api/users", new { id=await sender.Send(command) });
    [HttpPut("{id:guid}")]
    public async Task<TenantUserDto> Update(Guid id, UpdateTenantUserRequest request)
    {
        if(id!=request.Id) throw new InvalidOperationException("El usuario no coincide.");
        var m=await context.TenantMemberships.Include(x=>x.User).SingleAsync(x=>x.UserId==id&&x.TenantId==request.TenantId);
        if (m.Role == Roles.Owner || request.Role == Roles.Owner) throw new InvalidOperationException("La titularidad del negocio no se cambia desde este formulario.");
        m.User!.FirstName=request.FirstName.Trim();m.User.LastName=request.LastName.Trim();m.Role=request.Role;
        await ReplaceAssignments(id, request.TenantId, request.Role, request.WarehouseIds);
        await context.SaveChangesAsync();
        return new(m.UserId,m.User.FirstName,m.User.LastName,m.User.Email,m.Role,m.IsActive&&m.User.IsActive, request.Role is Roles.Owner or Roles.Admin ? [] : (request.WarehouseIds ?? []).Distinct().ToList());
    }
    [HttpPut("{id:guid}/warehouses")]
    public async Task<IActionResult> UpdateWarehouses(Guid id, UpdateUserWarehousesRequest request)
    {
        var membership = await context.TenantMemberships.SingleAsync(x => x.UserId == id && x.TenantId == request.TenantId);
        if (membership.Role is Roles.Owner or Roles.Admin) throw new InvalidOperationException("Los administradores ya tienen acceso a todas las sucursales.");
        await ReplaceAssignments(id, request.TenantId, membership.Role, request.WarehouseIds);
        await context.SaveChangesAsync();
        return NoContent();
    }
    [HttpPut("{id:guid}/toggle-status")] public async Task<IActionResult> Toggle(Guid id, ToggleTenantUserRequest request) { var m=await context.TenantMemberships.SingleAsync(x=>x.UserId==id&&x.TenantId==request.TenantId);if (m.Role == Roles.Owner) throw new InvalidOperationException("No se puede desactivar al titular del negocio."); m.IsActive=request.IsActive;await context.SaveChangesAsync();return NoContent(); }

    private async Task ReplaceAssignments(Guid userId, Guid tenantId, string role, IReadOnlyList<Guid>? requested)
    {
        var ids = (requested ?? []).Distinct().ToArray();
        if (role is Roles.Seller or Roles.Warehouse)
        {
            if (ids.Length == 0) throw new InvalidOperationException("Asigná al menos una sucursal al cajero u operador de stock.");
            var valid = await context.Warehouses.CountAsync(x => ids.Contains(x.Id) && x.TenantId == tenantId && x.IsActive);
            if (valid != ids.Length) throw new InvalidOperationException("Una o más sucursales no existen o están inactivas.");
        }
        await context.UserWarehouses.Where(x => x.UserId == userId && x.TenantId == tenantId).ExecuteDeleteAsync();
        if (role is Roles.Seller or Roles.Warehouse)
            context.UserWarehouses.AddRange(ids.Select(warehouseId => new UserWarehouse { Id = Guid.NewGuid(), UserId = userId, TenantId = tenantId, WarehouseId = warehouseId }));
    }
}
