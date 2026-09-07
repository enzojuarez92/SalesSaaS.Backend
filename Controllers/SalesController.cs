using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
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

    [HttpGet("customers/{customerId:guid}/account")]
    public async Task<IReadOnlyList<CustomerAccountEntryDto>> GetAccount(Guid customerId, [FromQuery] Guid tenantId) => await mediator.Send(new GetCustomerAccountQuery(tenantId, customerId));
}
