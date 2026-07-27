using Microsoft.AspNetCore.Mvc;
using MediatR;
using SalesSaaS.Features.Customers.Commands;
using FluentValidation; // 👈 Asegurate de tener este using

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
}