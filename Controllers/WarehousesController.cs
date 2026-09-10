using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Inventory.Warehouses;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/warehouses")]
[Authorize]
public sealed class WarehousesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IActionResult> Create(CreateWarehouseCommand command) =>
        Created($"/api/warehouses/{await mediator.Send(command)}", null);

    [HttpGet]
    [Authorize(Roles = Roles.Sales + "," + Roles.Warehouse)]
    public async Task<IReadOnlyList<WarehouseDto>> GetAll([FromQuery] Guid tenantId) =>
        await mediator.Send(new GetWarehousesQuery(tenantId));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<ActionResult<WarehouseDto>> Update(Guid id, UpdateWarehouseCommand command)
    {
        if (id != command.Id) return BadRequest("El identificador del depósito no coincide.");
        return Ok(await mediator.Send(command));
    }
}
