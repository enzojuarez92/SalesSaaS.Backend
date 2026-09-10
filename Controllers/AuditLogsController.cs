using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Auditing;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = Roles.Administration)]
public sealed class AuditLogsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<AuditLogDto>> Get([FromQuery] Guid tenantId, [FromQuery] string? entityName, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int take = 100, [FromQuery] Guid? warehouseId = null) =>
        await mediator.Send(new GetAuditLogsQuery(tenantId, entityName, fromUtc, toUtc, take, warehouseId));
}
