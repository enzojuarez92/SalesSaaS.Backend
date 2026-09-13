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
            .Select(o => new { o.Id, o.SupplierId, o.Status, o.TotalAmount, o.CreatedAtUtc, o.CreatedByUserId, Invoiced = db.PurchaseInvoices.Any(i => i.PurchaseOrderId == o.Id), Items = o.Items.Select(i => new { i.ProductId, i.Quantity, i.UnitCost }) }).ToListAsync(ct));
    }

    [HttpGet("orders/{purchaseOrderId:guid}")]
    public async Task<IActionResult> Detail(Guid purchaseOrderId, CancellationToken ct) => Ok(await db.PurchaseOrders.AsNoTracking().Where(o => o.Id == purchaseOrderId).Select(o => new { o.Id, o.SupplierId, o.Status, o.TotalAmount, o.CreatedAtUtc, o.CreatedByUserId, Items = o.Items.Select(i => new { i.ProductId, Product = db.Products.Where(p => p.Id == i.ProductId).Select(p => p.Name).FirstOrDefault(), Sku = db.Products.Where(p => p.Id == i.ProductId).Select(p => p.Sku).FirstOrDefault(), i.Quantity, i.UnitCost, i.TotalAmount }) }).SingleOrDefaultAsync(ct));
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

    [HttpPost("orders/{purchaseOrderId:guid}/authorize")]
    public async Task<IActionResult> Authorize(Guid purchaseOrderId, AuthorizePurchaseOrderCommand command)
    {
        if (purchaseOrderId != command.PurchaseOrderId) return BadRequest("El identificador de la orden no coincide.");
        await mediator.Send(command);
        return NoContent();
    }
    [HttpPost("orders/{purchaseOrderId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid purchaseOrderId, CancelPurchaseOrderCommand command)
    {
        if (purchaseOrderId != command.PurchaseOrderId) return BadRequest("El identificador de la orden no coincide.");
        await mediator.Send(command);
        return NoContent();
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice(CreatePurchaseInvoiceCommand command) =>
        Created($"/api/purchases/invoices/{await mediator.Send(command)}", null);
    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices(CancellationToken ct) => Ok(await db.PurchaseInvoices.AsNoTracking().OrderByDescending(invoice => invoice.IssuedAtUtc).Select(invoice => new { invoice.Id, invoice.PurchaseOrderId, invoice.SupplierId, Supplier = db.Suppliers.Where(supplier => supplier.Id == invoice.SupplierId).Select(supplier => supplier.LegalName).FirstOrDefault(), invoice.Number, invoice.TotalAmount, invoice.IssuedAtUtc }).ToListAsync(ct));
    [HttpGet("invoices/{invoiceId:guid}")]
    public async Task<IActionResult> InvoiceDetail(Guid invoiceId, CancellationToken ct) => Ok(await db.PurchaseInvoices.AsNoTracking().Where(invoice => invoice.Id == invoiceId).Select(invoice => new { invoice.Id, invoice.Number, invoice.TotalAmount, invoice.IssuedAtUtc, Supplier = db.Suppliers.Where(supplier => supplier.Id == invoice.SupplierId).Select(supplier => supplier.LegalName).FirstOrDefault(), Items = db.PurchaseOrderItems.Where(item => item.PurchaseOrderId == invoice.PurchaseOrderId).Select(item => new { item.ProductId, Product = db.Products.Where(product => product.Id == item.ProductId).Select(product => product.Name).FirstOrDefault(), Sku = db.Products.Where(product => product.Id == item.ProductId).Select(product => product.Sku).FirstOrDefault(), item.Quantity, item.UnitCost, item.TotalAmount }) }).SingleOrDefaultAsync(ct));
}
