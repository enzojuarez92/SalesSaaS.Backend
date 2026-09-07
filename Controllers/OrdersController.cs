using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
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
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderCommand command)
    {
        if (command == null)
        {
            return BadRequest("El cuerpo de la petición no puede ser nulo.");
        }

        var orderId = await _mediator.Send(command);
        return Created($"/api/orders/{orderId}", new { id = orderId });
    }

    [HttpPost("{orderId:guid}/cancel")]
    [Authorize(Roles = Roles.Sales)]
    public async Task<IActionResult> Cancel(Guid orderId, CancelOrderCommand command)
    {
        if (orderId != command.OrderId) return BadRequest("El ID del pedido no coincide con la ruta.");
        await _mediator.Send(command);
        return NoContent();
    }
}
