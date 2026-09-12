using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Features.Inventory.Warehouses;
using SalesSaaS.Application.Security;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/warehouses")]
[Authorize]
public sealed class WarehousesController(IMediator mediator, ApplicationDbContext context, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IActionResult> Create(CreateWarehouseCommand command) =>
        Created($"/api/warehouses/{await mediator.Send(command)}", null);

    [HttpGet]
    [Authorize(Roles = Roles.Sales + "," + Roles.Warehouse)]
    public async Task<IReadOnlyList<WarehouseDto>> GetAll([FromQuery] Guid tenantId)
    {
        var warehouses = await mediator.Send(new GetWarehousesQuery(tenantId));
        if (User.IsInRole(Roles.Owner) || User.IsInRole(Roles.Admin)) return warehouses;
        var assignedIds = await context.UserWarehouses.AsNoTracking().Where(item => item.UserId == currentUser.UserId && item.TenantId == tenantId).Select(item => item.WarehouseId).ToListAsync();
        return warehouses.Where(item => assignedIds.Contains(item.Id)).ToList();
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<ActionResult<WarehouseDto>> Update(Guid id, UpdateWarehouseCommand command)
    {
        if (id != command.Id) return BadRequest("El identificador del depósito no coincide.");
        return Ok(await mediator.Send(command));
    }
}
