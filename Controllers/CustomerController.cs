using Microsoft.AspNetCore.Mvc;
using MediatR;
using SalesSaaS.Features.Customers.Commands;
using FluentValidation;
using SalesSaaS.Features.Customers.Queries; // 👈 Asegurate de tener este using

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
    public async Task<IActionResult> GetCustomers(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            if (tenantId == Guid.Empty)
            {
                return BadRequest("El TenantId es obligatorio para consultar clientes.");
            }

            var query = new GetCustomersQuery(tenantId, searchTerm, isActive, pageNumber, pageSize);
            var result = await _mediator.Send(query);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    // GET: api/customers/{id}?tenantId={tenantId}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCustomerById([FromRoute] Guid id, [FromQuery] Guid tenantId)
    {
        try
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    // POST: api/customers
    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerCommand command)
    {
        try
        {
            var customerId = await _mediator.Send(command);
            return Created($"/api/customers/{customerId}", new { id = customerId });
        }
        catch (FluentValidation.ValidationException ex) 
        {
            var errors = ex.Errors.Select(e => new
            {
                field = e.PropertyName,
                error = e.ErrorMessage
            });

            return BadRequest(new { errors });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    // PUT: api/customers/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] UpdateCustomerCommand command)
    {
        if (id != command.Id)
        {
            return BadRequest("El ID del cliente en la ruta no coincide con el ID en el cuerpo de la solicitud.");
        }

        try
        {
            var updatedCustomer = await _mediator.Send(command);

            if (updatedCustomer == null)
            {
                return NotFound();
            }

            return Ok(updatedCustomer); // 👈 Retorna un 200 OK con el objeto Customer completo
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errors = ex.Errors.Select(e => new
            {
                field = e.PropertyName,
                error = e.ErrorMessage
            });

            return BadRequest(new { errors });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }

    // DELETE: api/customers/{id}?tenantId={tenantId}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer([FromRoute] Guid id, [FromQuery] Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            return BadRequest("El TenantId es obligatorio.");
        }

        try
        {
            var result = await _mediator.Send(new DeleteCustomerCommand(id, tenantId));

            if (!result)
            {
                return NotFound("El cliente no fue encontrado o no pertenece al Tenant especificado.");
            }

            return NoContent(); 
        }
        catch (Exception)
        {
            return StatusCode(500, "Ocurrió un error interno al procesar la solicitud.");
        }
    }
}