using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Catalog.Brands;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/brands")]
[Authorize(Roles = Roles.Inventory)]
public sealed class BrandsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateBrandCommand command) =>
        Created($"/api/brands/{await mediator.Send(command)}", null);

    [HttpGet]
    public async Task<IReadOnlyList<BrandDto>> GetAll([FromQuery] Guid tenantId) =>
        await mediator.Send(new GetBrandsQuery(tenantId));
}
