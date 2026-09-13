using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Application.Common;
using SalesSaaS.Application.Security;
using SalesSaaS.Features.Sales;
using SalesSaaS.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize(Roles = Roles.Sales)]
public sealed class SalesController(IMediator mediator, ApplicationDbContext db, ICurrentUser currentUser) : ControllerBase
{
    public sealed record SalesHistoryRow(Guid Id, DateTime Date, string ReceiptNumber, string Customer, string Seller, PaymentMethod PaymentMethod, decimal Total, string Status);
    public sealed record SalesHistoryPaymentTotal(PaymentMethod PaymentMethod, decimal Total);
    public sealed record SalesHistorySeller(Guid Id, string Name);
    public sealed record SaleDetailRow(Guid Id, DateTime Date, string ReceiptNumber, string Customer, string CustomerDocument, string Seller, PaymentMethod PaymentMethod, decimal Total, decimal Discount, string Status, IReadOnlyList<SaleItemRow> Items);
    public sealed record SaleItemRow(string Product, string Sku, int Quantity, decimal UnitPrice, decimal Subtotal);

    [HttpGet("history")]
    public async Task<PagedResult<SalesHistoryRow>> History([FromQuery] Guid tenantId, [FromQuery] Guid? warehouseId, [FromQuery] Guid? sellerId, [FromQuery] PaymentMethod? paymentMethod, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Orders.AsNoTracking().Where(order => order.TenantId == tenantId);
        if (warehouseId.HasValue) query = query.Where(order => order.WarehouseId == warehouseId.Value);
        if (sellerId.HasValue) query = query.Where(order => order.SellerId == sellerId.Value);
        if (paymentMethod.HasValue) query = query.Where(order => order.PaymentMethod == paymentMethod.Value);
        if (fromUtc.HasValue) query = query.Where(order => order.OrderDate >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(order => order.OrderDate <= toUtc.Value);
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(order => order.OrderDate).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(order => new SalesHistoryRow(order.Id, order.OrderDate,
                db.Invoices.Where(invoice => invoice.OrderId == order.Id).OrderByDescending(invoice => invoice.IssuedAtUtc).Select(invoice => invoice.Number).FirstOrDefault() ?? $"POS-{order.Id:N}",
                order.Customer!.Name,
                order.Seller == null ? "Sin registrar" : order.Seller.FirstName + " " + order.Seller.LastName,
                order.PaymentMethod, order.TotalAmount, order.Status))
            .ToListAsync(ct);
        return new PagedResult<SalesHistoryRow>(items, totalCount, pageNumber, pageSize);
    }

    [HttpGet("history/totals")]
    public async Task<IReadOnlyList<SalesHistoryPaymentTotal>> HistoryTotals([FromQuery] Guid tenantId, [FromQuery] Guid? warehouseId, [FromQuery] Guid? sellerId, [FromQuery] PaymentMethod? paymentMethod, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking().Where(order => order.TenantId == tenantId && order.Status != "Cancelled");
        if (warehouseId.HasValue) query = query.Where(order => order.WarehouseId == warehouseId.Value);
        if (sellerId.HasValue) query = query.Where(order => order.SellerId == sellerId.Value);
        if (paymentMethod.HasValue) query = query.Where(order => order.PaymentMethod == paymentMethod.Value);
        if (fromUtc.HasValue) query = query.Where(order => order.OrderDate >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(order => order.OrderDate <= toUtc.Value);
        return await query.GroupBy(order => order.PaymentMethod)
            .Select(group => new SalesHistoryPaymentTotal(group.Key, group.Sum(order => order.TotalAmount)))
            .ToListAsync(ct);
    }

    [HttpGet("history/sellers")]
    public async Task<IReadOnlyList<SalesHistorySeller>> HistorySellers([FromQuery] Guid tenantId, CancellationToken ct) =>
        await db.TenantMemberships.AsNoTracking().Where(membership => membership.TenantId == tenantId && membership.IsActive && membership.User!.IsActive)
            .OrderBy(membership => membership.User!.FirstName).ThenBy(membership => membership.User!.LastName)
            .Select(membership => new SalesHistorySeller(membership.UserId, (membership.User!.FirstName + " " + membership.User.LastName).Trim()))
            .ToListAsync(ct);

    [HttpGet("history/{id:guid}")]
    public async Task<ActionResult<SaleDetailRow>> HistoryDetail(Guid id, [FromQuery] Guid tenantId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().Include(item => item.Customer).Include(item => item.Seller).Include(item => item.Items).ThenInclude(item => item.Product)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId, ct);
        if (order is null) return NotFound();
        var receipt = await db.Invoices.AsNoTracking().Where(invoice => invoice.OrderId == id).OrderByDescending(invoice => invoice.IssuedAtUtc).Select(invoice => invoice.Number).FirstOrDefaultAsync(ct) ?? $"POS-{order.Id:N}";
        return new SaleDetailRow(order.Id, order.OrderDate, receipt, order.Customer!.Name, $"{order.Customer.DocumentType} {order.Customer.DocumentNumber}".Trim(), order.Seller == null ? "Sin registrar" : $"{order.Seller.FirstName} {order.Seller.LastName}", order.PaymentMethod, order.TotalAmount, order.DiscountAmount, order.Status,
            order.Items.Select(item => new SaleItemRow(item.Product?.Name ?? "Producto eliminado", item.Product?.Sku ?? "—", item.Quantity, item.UnitPrice, item.SubTotal)).ToList());
    }

    [HttpGet("quotes")]
    public async Task<IActionResult> Quotes(CancellationToken ct) => Ok(await db.Quotes.AsNoTracking().OrderByDescending(q => q.CreatedAtUtc).Take(100)
        .Select(q => new { q.Id, q.CustomerId, Customer = q.Customer!.Name, q.TotalAmount, q.ExpiresAtUtc, q.Status, Items = q.Items.Select(i => new { i.ProductId, i.Quantity }) }).ToListAsync(ct));

    [HttpGet("quotes/{id:guid}")]
    public async Task<IActionResult> Quote(Guid id, CancellationToken ct)
    {
        var quote = await db.Quotes.AsNoTracking().Where(q => q.Id == id).Select(q => new { q.Id, q.CustomerId, q.ExpiresAtUtc, Items = q.Items.Select(i => new { i.ProductId, i.Quantity }) }).SingleOrDefaultAsync(ct);
        return quote is null ? NotFound() : Ok(quote);
    }
    [HttpPost("quotes")]
    public async Task<IActionResult> CreateQuote(CreateQuoteCommand command) => Created($"/api/sales/quotes/{await mediator.Send(command)}", null);

    [HttpPost("quotes/{id:guid}/cancel")]
    public async Task<IActionResult> CancelQuote(Guid id, CancellationToken ct)
    {
        if (!currentUser.TenantId.HasValue) return Unauthorized();
        var quote = await db.Quotes.SingleOrDefaultAsync(
            item => item.Id == id && item.TenantId == currentUser.TenantId.Value,
            ct);
        if (quote is null) return NotFound(new { message = "El presupuesto no existe o no pertenece al negocio activo." });
        if (!string.Equals(quote.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { message = "Sólo se pueden anular presupuestos vigentes." });

        quote.Status = "Cancelled";
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice(CreateInvoiceFromOrderCommand command) => Created($"/api/sales/invoices/{await mediator.Send(command)}", null);

    [HttpGet("invoices")]
    public async Task<PagedResult<InvoiceDto>> GetInvoices([FromQuery] Guid tenantId, [FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] DateOnly? dateFrom = null, [FromQuery] DateOnly? dateTo = null, [FromQuery] string? customer = null) =>
        await mediator.Send(new GetInvoicesQuery(tenantId, status, pageNumber, pageSize, dateFrom, dateTo, customer));

    [HttpGet("customers/{customerId:guid}/account")]
    public async Task<IReadOnlyList<CustomerAccountEntryDto>> GetAccount(Guid customerId, [FromQuery] Guid tenantId) => await mediator.Send(new GetCustomerAccountQuery(tenantId, customerId));
}
