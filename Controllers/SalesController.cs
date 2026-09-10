using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Application.Common;
using SalesSaaS.Features.Sales;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize(Roles = Roles.Sales)]
public sealed class SalesController(IMediator mediator) : ControllerBase
{
    [HttpPost("quotes")]
    public async Task<IActionResult> CreateQuote(CreateQuoteCommand command) => Created($"/api/sales/quotes/{await mediator.Send(command)}", null);

    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice(CreateInvoiceFromOrderCommand command) => Created($"/api/sales/invoices/{await mediator.Send(command)}", null);

    [HttpGet("invoices")]
    public async Task<PagedResult<InvoiceDto>> GetInvoices([FromQuery] Guid tenantId, [FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] DateOnly? dateFrom = null, [FromQuery] DateOnly? dateTo = null, [FromQuery] string? customer = null) =>
        await mediator.Send(new GetInvoicesQuery(tenantId, status, pageNumber, pageSize, dateFrom, dateTo, customer));

    [HttpGet("customers/{customerId:guid}/account")]
    public async Task<IReadOnlyList<CustomerAccountEntryDto>> GetAccount(Guid customerId, [FromQuery] Guid tenantId) => await mediator.Send(new GetCustomerAccountQuery(tenantId, customerId));
}
