using MediatR;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Features.Orders.Commands;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // 🚀 POST: api/orders
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
    {
        if (command == null)
        {
            return BadRequest("El cuerpo de la petición no puede ser nulo.");
        }

        try
        {
            var orderId = await _mediator.Send(command);
            return Created($"/api/orders/{orderId}", new { id = orderId });
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