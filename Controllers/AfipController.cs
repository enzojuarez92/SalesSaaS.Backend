using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Afip;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/afip")]
[Authorize(Roles = Roles.Administration)]
public sealed class AfipController(IMediator mediator) : ControllerBase
{
    [HttpPut("fiscal-profile")]
    public async Task<IActionResult> ConfigureFiscalProfile(ConfigureTenantFiscalProfileCommand command) =>
        Ok(new { id = await mediator.Send(command) });

    [HttpGet("fiscal-profile")]
    public async Task<TenantFiscalProfileDto?> GetFiscalProfile([FromQuery] Guid tenantId) =>
        await mediator.Send(new GetTenantFiscalProfileQuery(tenantId));

    [HttpPost("invoices/{invoiceId:guid}/authorize")]
    public async Task<ActionResult<AfipInvoiceAuthorizationDto>> AuthorizeInvoice(Guid invoiceId, AuthorizeInvoiceCommand command)
    {
        if (invoiceId != command.InvoiceId) return BadRequest("El identificador de la factura no coincide.");
        return Ok(await mediator.Send(command));
    }
}
