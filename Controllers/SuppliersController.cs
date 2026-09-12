using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Purchases;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public sealed class SuppliersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.Administration)]
    public async Task<IActionResult> Create(CreateSupplierCommand command) =>
        Created($"/api/suppliers/{await mediator.Send(command)}", null);

    [HttpPut("{supplierId:guid}")]
    [Authorize(Roles = Roles.Administration)]
    public async Task<SupplierDto> Update(Guid supplierId, UpdateSupplierCommand command)
    {
        if (supplierId != command.Id) throw new InvalidOperationException("El proveedor no coincide.");
        return await mediator.Send(command);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IReadOnlyList<SupplierDto>> GetAll([FromQuery] Guid tenantId) =>
        await mediator.Send(new GetSuppliersQuery(tenantId));

    [HttpGet("{supplierId:guid}/account")]
    [Authorize(Roles = Roles.Administration)]
    public async Task<IReadOnlyList<SupplierAccountEntryDto>> GetAccount(Guid supplierId, [FromQuery] Guid tenantId) =>
        await mediator.Send(new GetSupplierAccountQuery(tenantId, supplierId));
}
