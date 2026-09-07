using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Catalog.Categories;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize(Roles = Roles.Inventory)]
public sealed class CategoriesController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateCategoryCommand command) =>
        Created($"/api/categories/{await mediator.Send(command)}", null);

    [HttpGet]
    public async Task<IReadOnlyList<CategoryDto>> GetAll([FromQuery] Guid tenantId) =>
        await mediator.Send(new GetCategoriesQuery(tenantId));
}
