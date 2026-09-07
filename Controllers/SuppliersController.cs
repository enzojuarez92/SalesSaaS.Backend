using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Purchases;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize(Roles = Roles.Administration)]
public sealed class SuppliersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateSupplierCommand command) =>
        Created($"/api/suppliers/{await mediator.Send(command)}", null);

    [HttpGet]
    public async Task<IReadOnlyList<SupplierDto>> GetAll([FromQuery] Guid tenantId) =>
        await mediator.Send(new GetSuppliersQuery(tenantId));

    [HttpGet("{supplierId:guid}/account")]
    public async Task<IReadOnlyList<SupplierAccountEntryDto>> GetAccount(Guid supplierId, [FromQuery] Guid tenantId) =>
        await mediator.Send(new GetSupplierAccountQuery(tenantId, supplierId));
}
