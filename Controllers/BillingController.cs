using MediatR;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SalesSaaS.Domain;
using SalesSaaS.Features.Billing;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize]
[EnableRateLimiting("billing")]
public sealed class BillingController(IMediator mediator) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetPlans() => await mediator.Send(new GetSubscriptionPlansQuery());

    [HttpPost("plans")]
    [Authorize(Policy = "PlatformAdmin")]
    public async Task<IActionResult> CreatePlan(CreateSubscriptionPlanCommand command) =>
        Created($"/api/billing/plans/{await mediator.Send(command)}", null);

    [HttpPost("subscriptions")]
    [Authorize(Roles = Roles.Administration)]
    public async Task<ActionResult<SubscriptionCheckoutDto>> Subscribe(SubscribeTenantCommand command) => Ok(await mediator.Send(command));

    [HttpGet("subscriptions/current")]
    public async Task<TenantSubscriptionDto?> GetCurrentSubscription([FromQuery] Guid tenantId) => await mediator.Send(new GetTenantSubscriptionQuery(tenantId));

    [HttpPost("webhooks/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessWebhook(string provider, [FromBody] JsonElement payload, [FromHeader(Name = "X-Payment-Signature")] string? signature)
    {
        await mediator.Send(new ProcessPaymentWebhookCommand(provider, payload.GetRawText(), signature));
        return Ok(new { message = "Webhook procesado correctamente." });
    }
}
