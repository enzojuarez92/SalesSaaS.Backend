using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Purchases;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize(Roles = Roles.Inventory)]
public sealed class PurchasesController(IMediator mediator) : ControllerBase
{
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(CreatePurchaseOrderCommand command) =>
        Created($"/api/purchases/orders/{await mediator.Send(command)}", null);

    [HttpPost("orders/{purchaseOrderId:guid}/receive")]
    public async Task<IActionResult> Receive(Guid purchaseOrderId, ReceivePurchaseOrderCommand command)
    {
        if (purchaseOrderId != command.PurchaseOrderId) return BadRequest("El identificador de la orden no coincide.");
        await mediator.Send(command);
        return NoContent();
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice(CreatePurchaseInvoiceCommand command) =>
        Created($"/api/purchases/invoices/{await mediator.Send(command)}", null);
}
