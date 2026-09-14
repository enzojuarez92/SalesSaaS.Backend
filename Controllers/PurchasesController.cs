using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Purchases;
using SalesSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Common;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize(Roles = Roles.Inventory)]
public sealed class PurchasesController(IMediator mediator, ApplicationDbContext db) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<IActionResult> List(CancellationToken ct, int page = 1, int pageSize = 15)
    {
        if (page < 1) return BadRequest();
        pageSize = Math.Clamp(pageSize, 1, 15);
        var query = db.PurchaseOrders.AsNoTracking();
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new { o.Id, o.SupplierId, Status = db.PurchaseInvoices.Any(i => i.PurchaseOrderId == o.Id) ? "Invoiced" : o.Status, o.TotalAmount, o.CreatedAtUtc, o.CreatedByUserId, Invoiced = db.PurchaseInvoices.Any(i => i.PurchaseOrderId == o.Id), Items = o.Items.Select(i => new { i.ProductId, i.Quantity, i.UnitCost }).ToList() }).ToListAsync(ct);
        return Ok(new PagedResult<object>(items.Cast<object>().ToList(), totalCount, page, pageSize));
    }

    [HttpGet("orders/{purchaseOrderId:guid}")]
    public async Task<IActionResult> Detail(Guid purchaseOrderId, CancellationToken ct) => Ok(await db.PurchaseOrders.AsNoTracking().Where(o => o.Id == purchaseOrderId).Select(o => new { o.Id, o.SupplierId, Status = db.PurchaseInvoices.Any(i => i.PurchaseOrderId == o.Id) ? "Invoiced" : o.Status, o.TotalAmount, o.CreatedAtUtc, o.CreatedByUserId, Items = o.Items.Select(i => new { i.ProductId, Product = db.Products.Where(p => p.Id == i.ProductId).Select(p => p.Name).FirstOrDefault(), Sku = db.Products.Where(p => p.Id == i.ProductId).Select(p => p.Sku).FirstOrDefault(), i.Quantity, i.UnitCost, i.TotalAmount }).ToList() }).SingleOrDefaultAsync(ct));
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
    public async Task<IActionResult> Invoices(CancellationToken ct, int page = 1, int pageSize = 15)
    {
        if (page < 1) return BadRequest();
        pageSize = Math.Clamp(pageSize, 1, 15);
        var query = db.PurchaseInvoices.AsNoTracking();
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(invoice => invoice.IssuedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).Select(invoice => new { invoice.Id, invoice.PurchaseOrderId, invoice.SupplierId, Supplier = db.Suppliers.Where(supplier => supplier.Id == invoice.SupplierId).Select(supplier => supplier.LegalName).FirstOrDefault(), invoice.Number, invoice.TotalAmount, invoice.IssuedAtUtc, HasAttachment = invoice.AttachmentData != null }).ToListAsync(ct);
        return Ok(new PagedResult<object>(items.Cast<object>().ToList(), totalCount, page, pageSize));
    }
    [HttpGet("invoices/{invoiceId:guid}")]
    public async Task<IActionResult> InvoiceDetail(Guid invoiceId, CancellationToken ct) => Ok(await db.PurchaseInvoices.AsNoTracking().Where(invoice => invoice.Id == invoiceId).Select(invoice => new { invoice.Id, invoice.Number, invoice.TotalAmount, invoice.IssuedAtUtc, HasAttachment = invoice.AttachmentData != null, Supplier = db.Suppliers.Where(supplier => supplier.Id == invoice.SupplierId).Select(supplier => supplier.LegalName).FirstOrDefault(), Items = db.PurchaseOrderItems.Where(item => item.PurchaseOrderId == invoice.PurchaseOrderId).Select(item => new { item.ProductId, Product = db.Products.Where(product => product.Id == item.ProductId).Select(product => product.Name).FirstOrDefault(), Sku = db.Products.Where(product => product.Id == item.ProductId).Select(product => product.Sku).FirstOrDefault(), item.Quantity, item.UnitCost, item.TotalAmount }).ToList() }).SingleOrDefaultAsync(ct));

    [HttpPost("invoices/{invoiceId:guid}/attachment")]
    public async Task<IActionResult> UploadAttachment(Guid invoiceId, IFormFile file, CancellationToken ct)
    {
        const long maxFileBytes = 10 * 1024 * 1024;
        if (file.Length == 0 || file.Length > maxFileBytes) return BadRequest("El archivo debe ser un PDF de hasta 10 MB.");
        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return BadRequest("Sólo se permiten archivos PDF.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var content = stream.ToArray();
        if (content.Length < 4 || !content.AsSpan(0, 4).SequenceEqual("%PDF"u8)) return BadRequest("El archivo adjunto no es un PDF válido.");

        var invoice = await db.PurchaseInvoices.SingleOrDefaultAsync(item => item.Id == invoiceId, ct);
        if (invoice is null) return NotFound();
        invoice.AttachmentFileName = Path.GetFileName(file.FileName);
        invoice.AttachmentContentType = "application/pdf";
        invoice.AttachmentData = content;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("invoices/{invoiceId:guid}/attachment")]
    public async Task<IActionResult> DownloadAttachment(Guid invoiceId, CancellationToken ct)
    {
        var invoice = await db.PurchaseInvoices.AsNoTracking().Where(item => item.Id == invoiceId && item.AttachmentData != null).Select(item => new { item.AttachmentData, item.AttachmentContentType, item.AttachmentFileName }).SingleOrDefaultAsync(ct);
        return invoice is null ? NotFound() : File(invoice.AttachmentData!, invoice.AttachmentContentType ?? "application/pdf", invoice.AttachmentFileName ?? "factura-proveedor.pdf");
    }
}
