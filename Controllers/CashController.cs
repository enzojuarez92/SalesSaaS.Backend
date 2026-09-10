using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.CashRegisters;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/cash")]
[Authorize(Roles = Roles.Sales)]
public sealed class CashController(IMediator mediator) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<CashSessionDto>> Current([FromQuery] Guid tenantId, [FromQuery] Guid? warehouseId)
    {
        if (tenantId == Guid.Empty) return BadRequest("El TenantId es obligatorio.");
        var session = await mediator.Send(new GetCurrentCashSessionQuery(tenantId, warehouseId));
        return session is null ? NoContent() : Ok(session);
    }

    [HttpGet("history")]
    public async Task<IReadOnlyList<CashSessionDto>> History([FromQuery] Guid tenantId, [FromQuery] int take = 20) =>
        await mediator.Send(new GetCashSessionHistoryQuery(tenantId, Math.Clamp(take, 1, 50)));

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashRegisterSessionCommand command) =>
        Created($"/api/cash/current?tenantId={command.TenantId}", new { id = await mediator.Send(command) });

    [HttpPost("close")]
    public async Task<ActionResult<CashCloseResultDto>> Close([FromBody] CloseCashRegisterSessionCommand command) =>
        Ok(await mediator.Send(command));

    [HttpPost("movements")]
    public async Task<IActionResult> Movement([FromBody] RecordCashMovementCommand command) =>
        Created($"/api/cash/movements/{await mediator.Send(command)}", null);
}
