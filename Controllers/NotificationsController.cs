using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Features.Notifications;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<NotificationDto>> GetMine([FromQuery] bool unreadOnly = false)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar al usuario actual.");
        return await mediator.Send(new GetMyNotificationsQuery(currentUser.TenantId.Value, currentUser.UserId.Value, unreadOnly));
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar al usuario actual.");
        await mediator.Send(new MarkNotificationReadCommand(currentUser.TenantId.Value, currentUser.UserId.Value, notificationId));
        return NoContent();
    }

    [HttpPost("account-receivable-reminders")]
    [Authorize(Roles = Roles.Administration)]
    public async Task<IActionResult> SendAccountReminders([FromQuery] Guid tenantId) => Ok(new { sent = await mediator.Send(new SendAccountReceivableRemindersCommand(tenantId)) });

    [HttpPost("subscription-expiration-alerts")]
    [Authorize(Roles = Roles.Administration)]
    public async Task<IActionResult> SendSubscriptionAlerts([FromQuery] Guid tenantId, [FromQuery] int daysBeforeExpiration = 7) => Ok(new { sent = await mediator.Send(new SendSubscriptionExpirationAlertsCommand(tenantId, daysBeforeExpiration)) });
}
