using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Inventory.Stock;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/stock-movements")]
[Authorize(Roles = Roles.Inventory)]
public sealed class StockMovementsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Record(RecordStockMovementCommand command) =>
        Created($"/api/stock-movements/{await mediator.Send(command)}", null);

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer(TransferStockCommand command) =>
        Created(string.Empty, new { productId = await mediator.Send(command) });

    [HttpGet]
    public async Task<IReadOnlyList<StockMovementDto>> GetAll([FromQuery] Guid tenantId, [FromQuery] Guid productId, [FromQuery] Guid? warehouseId) =>
        await mediator.Send(new GetStockMovementsQuery(tenantId, productId, warehouseId));
}
