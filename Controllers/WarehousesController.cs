using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Inventory.Warehouses;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/warehouses")]
[Authorize(Roles = Roles.Inventory)]
public sealed class WarehousesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateWarehouseCommand command) =>
        Created($"/api/warehouses/{await mediator.Send(command)}", null);

    [HttpGet]
    public async Task<IReadOnlyList<WarehouseDto>> GetAll([FromQuery] Guid tenantId) =>
        await mediator.Send(new GetWarehousesQuery(tenantId));
}
