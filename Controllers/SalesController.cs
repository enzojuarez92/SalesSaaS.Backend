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
