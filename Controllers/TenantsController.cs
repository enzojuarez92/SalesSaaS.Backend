using MediatR;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Features.Tenants.Commands;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TenantsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // 🚀 POST: api/tenants
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command)
    {
        if (command == null)
        {
            return BadRequest("Los datos del negocio no pueden ser nulos.");
        }

        var tenantId = await _mediator.Send(command);

        return CreatedAtAction(nameof(Create), new { id = tenantId }, new { id = tenantId });
    }
}