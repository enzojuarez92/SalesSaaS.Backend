using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Billing;
using System.Text.Json;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
public sealed class SubscriptionsController(ISender sender) : ControllerBase
{
    [HttpGet("current")]
    public async Task<TenantSubscriptionDto?> Current([FromQuery] Guid tenantId) => await sender.Send(new GetTenantSubscriptionQuery(tenantId));

    [HttpGet("plans")]
    public async Task<IReadOnlyList<SubscriptionPlanDto>> Plans() => await sender.Send(new GetSubscriptionPlansQuery());

    [HttpPost("change-plan")]
    [Authorize(Roles = Roles.Administration)]
    public async Task<ActionResult<SubscriptionCheckoutDto>> ChangePlan([FromBody] SubscribeTenantCommand command) => Ok(await sender.Send(command));

    [HttpPost("checkout")]
    [Authorize(Roles = Roles.Administration)]
    public async Task<ActionResult<SubscriptionCheckoutDto>> Checkout([FromBody] SubscribeTenantCommand command) => Ok(await sender.Send(command));

    [HttpPost("mp-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> MercadoPagoWebhook([FromBody] JsonElement payload, [FromHeader(Name = "X-Signature")] string? signature)
    {
        await sender.Send(new ProcessPaymentWebhookCommand("MercadoPago", payload.GetRawText(), signature));
        return Ok(new { message = "Webhook de Mercado Pago procesado." });
    }
}
