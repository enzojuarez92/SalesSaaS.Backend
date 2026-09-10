using Microsoft.AspNetCore.Mvc;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using SalesSaaS.Features.Customers.Commands;
using SalesSaaS.Features.Customers.Queries; // 👈 Asegurate de tener este using
using SalesSaaS.Domain;
using SalesSaaS.Features.Customers;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET: api/customers?tenantId={tenantId}&searchTerm=juan&isActive=true&pageNumber=1&pageSize=10
    [HttpGet]
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio para consultar clientes.");
        }

        var query = new GetCustomersQuery(tenantId, searchTerm, isActive, pageNumber, pageSize);
        var result = await _mediator.Send(query);

        return Ok(result);
    }

    // GET: api/customers/{id}?tenantId={tenantId}
    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> GetCustomerById([FromRoute] Guid id, [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio para consultar un cliente.");
        }

        var customer = await _mediator.Send(new GetCustomerByIdQuery(id, tenantId));
        if (customer == null)
        {
            return NotFound();
        }
        return Ok(customer);
    }

    [HttpGet("{id:guid}/statement")]
    [Authorize(Roles = Roles.Sales)]
    public async Task<CustomerStatementDto> GetStatement(Guid id, [FromQuery] Guid tenantId) => await _mediator.Send(new GetCustomerStatementQuery(tenantId, id));

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordCustomerPaymentCommand command)
    {
        if (id != command.CustomerId) return BadRequest("El ID del cliente no coincide con la ruta.");
        return Created($"/api/customers/{id}/payments/{await _mediator.Send(command)}", null);
    }

    // POST: api/customers
    [HttpPost]
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerCommand command)
    {
        var customerId = await _mediator.Send(command);
        return Created($"/api/customers/{customerId}", new { id = customerId });
    }

    // PUT: api/customers/{id}
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] UpdateCustomerCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("El ID del cliente en la ruta no coincide con el ID en el cuerpo de la solicitud.");
        }

        var updatedCustomer = await _mediator.Send(command);

        if (updatedCustomer == null)
        {
            return NotFound();
        }

        return Ok(updatedCustomer);
    }

    // DELETE: api/customers/{id}?tenantId={tenantId}
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> DeleteCustomer([FromRoute] Guid id, [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio.");
        }

        var result = await _mediator.Send(new DeleteCustomerCommand(id, tenantId));

        if (!result)
        {
            return NotFound("El cliente no fue encontrado o no pertenece al Tenant especificado.");
        }

        return NoContent();
    }
}
