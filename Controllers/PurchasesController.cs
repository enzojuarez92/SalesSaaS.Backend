using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Purchases;
using SalesSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize(Roles = Roles.Inventory)]
public sealed class PurchasesController(IMediator mediator, ApplicationDbContext db) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1)
    {
        if (page < 1) return BadRequest();
        return Ok(await db.PurchaseOrders.AsNoTracking().OrderByDescending(o => o.CreatedAtUtc).Skip((page - 1) * 50).Take(50)
            .Select(o => new { o.Id, o.SupplierId, o.Status, o.TotalAmount, o.CreatedAtUtc, Invoiced = db.PurchaseInvoices.Any(i => i.PurchaseOrderId == o.Id), Items = o.Items.Select(i => new { i.ProductId, i.Quantity, i.UnitCost }) }).ToListAsync(ct));
    }
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
