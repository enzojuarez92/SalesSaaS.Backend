using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record PlatformNotificationDto(Guid Id, string Title, string Message, string Severity, Guid? TargetTenantId, string? TargetTenantName, bool IsActive, DateTime CreatedAtUtc);
public sealed record CreatePlatformNotificationRequest(string Title, string Message, string Severity, Guid? TargetTenantId);
public sealed record UpdatePlatformNotificationRequest(string Title, string Message, string Severity, Guid? TargetTenantId, bool IsActive);
public sealed record NotificationHistoryDto(Guid Id, string Source, string Title, string Message, string Severity, bool IsRead, DateTime CreatedAtUtc);
public sealed record PagedNotificationHistory(IReadOnlyList<NotificationHistoryDto> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);

[ApiController]
[Route("api/superadmin/notifications")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class SuperAdminNotificationsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<PlatformNotificationDto>> Get(CancellationToken cancellationToken) =>
        await (from notification in context.PlatformNotifications.AsNoTracking()
               join tenant in context.Tenants.IgnoreQueryFilters().AsNoTracking() on notification.TargetTenantId equals tenant.Id into tenants
               from tenant in tenants.DefaultIfEmpty()
               orderby notification.CreatedAtUtc descending
               select ToDto(notification, tenant == null ? null : tenant.Name)).ToListAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<PlatformNotificationDto>> Create([FromBody] CreatePlatformNotificationRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Title, request.Message, request.Severity);
        if (request.TargetTenantId.HasValue && !await context.Tenants.IgnoreQueryFilters().AnyAsync(tenant => tenant.Id == request.TargetTenantId, cancellationToken)) return BadRequest(new { message = "La empresa seleccionada no existe." });
        var notification = new PlatformNotification { Id = Guid.NewGuid(), Title = request.Title.Trim(), Message = request.Message.Trim(), Severity = request.Severity.Trim().ToLowerInvariant(), TargetTenantId = request.TargetTenantId };
        context.PlatformNotifications.Add(notification);
        await context.SaveChangesAsync(cancellationToken);
        var tenantName = request.TargetTenantId.HasValue ? await context.Tenants.IgnoreQueryFilters().Where(tenant => tenant.Id == request.TargetTenantId).Select(tenant => tenant.Name).SingleOrDefaultAsync(cancellationToken) : null;
        return Created($"/api/superadmin/notifications/{notification.Id}", ToDto(notification, tenantName));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlatformNotificationDto>> Update(Guid id, [FromBody] UpdatePlatformNotificationRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Title, request.Message, request.Severity);
        var notification = await context.PlatformNotifications.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (notification is null) return NotFound();
        if (request.TargetTenantId.HasValue && !await context.Tenants.IgnoreQueryFilters().AnyAsync(tenant => tenant.Id == request.TargetTenantId, cancellationToken)) return BadRequest(new { message = "La empresa seleccionada no existe." });
        notification.Title = request.Title.Trim(); notification.Message = request.Message.Trim(); notification.Severity = request.Severity.Trim().ToLowerInvariant(); notification.TargetTenantId = request.TargetTenantId; notification.IsActive = request.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        var tenantName = request.TargetTenantId.HasValue ? await context.Tenants.IgnoreQueryFilters().Where(tenant => tenant.Id == request.TargetTenantId).Select(tenant => tenant.Name).SingleOrDefaultAsync(cancellationToken) : null;
        return Ok(ToDto(notification, tenantName));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var notification = await context.PlatformNotifications.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (notification is null) return NotFound();
        context.PlatformNotifications.Remove(notification);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void Validate(string title, string message, string severity)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) throw new InvalidOperationException("El título es obligatorio y no puede superar los 200 caracteres.");
        if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 2000) throw new InvalidOperationException("El mensaje es obligatorio y no puede superar los 2000 caracteres.");
        if (severity.Trim().ToLowerInvariant() is not ("info" or "warning" or "success")) throw new InvalidOperationException("La severidad debe ser info, warning o success.");
    }
    private static PlatformNotificationDto ToDto(PlatformNotification item, string? tenantName) => new(item.Id, item.Title, item.Message, item.Severity, item.TargetTenantId, tenantName, item.IsActive, item.CreatedAtUtc);
}

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class PlatformNotificationInboxController(ApplicationDbContext context, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("unread")]
    public async Task<IReadOnlyList<PlatformNotificationDto>> Unread(CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar el negocio activo.");
        var tenantId = currentUser.TenantId.Value;
        return await context.PlatformNotifications.AsNoTracking().Where(item => item.IsActive && (item.TargetTenantId == null || item.TargetTenantId == tenantId)
            && !context.PlatformNotificationReads.Any(read => read.PlatformNotificationId == item.Id && read.TenantId == tenantId))
            .OrderByDescending(item => item.CreatedAtUtc).Select(item => new PlatformNotificationDto(item.Id, item.Title, item.Message, item.Severity, item.TargetTenantId, null, item.IsActive, item.CreatedAtUtc)).ToListAsync(cancellationToken);
    }

    [HttpGet("history")]
    public async Task<PagedNotificationHistory> History([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 15, CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar al usuario actual.");
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var tenantId = currentUser.TenantId.Value;
        var userId = currentUser.UserId.Value;
        var personal = await context.Notifications.AsNoTracking().Where(item => item.TenantId == tenantId && item.UserId == userId)
            .Select(item => new NotificationHistoryDto(item.Id, "personal", item.Title, item.Message, "info", item.IsRead, item.CreatedAtUtc)).ToListAsync(cancellationToken);
        var readNotificationIds = await context.PlatformNotificationReads.AsNoTracking().Where(item => item.TenantId == tenantId).Select(item => item.PlatformNotificationId).ToListAsync(cancellationToken);
        var platform = await context.PlatformNotifications.AsNoTracking().Where(item => item.TargetTenantId == null || item.TargetTenantId == tenantId)
            .Select(item => new { item.Id, item.Title, item.Message, item.Severity, item.CreatedAtUtc }).ToListAsync(cancellationToken);
        var all = personal.Concat(platform.Select(item => new NotificationHistoryDto(item.Id, "platform", item.Title, item.Message, item.Severity, readNotificationIds.Contains(item.Id), item.CreatedAtUtc)))
            .OrderByDescending(item => item.CreatedAtUtc).ToList();
        var totalCount = all.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var items = all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        return new PagedNotificationHistory(items, pageNumber, pageSize, totalCount, totalPages);
    }

    [HttpPost("platform/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar el negocio activo.");
        var tenantId = currentUser.TenantId.Value;
        var exists = await context.PlatformNotifications.AnyAsync(item => item.Id == id && item.IsActive && (item.TargetTenantId == null || item.TargetTenantId == tenantId), cancellationToken);
        if (!exists) return NotFound();
        if (!await context.PlatformNotificationReads.AnyAsync(item => item.PlatformNotificationId == id && item.TenantId == tenantId, cancellationToken))
        {
            context.PlatformNotificationReads.Add(new PlatformNotificationRead { Id = Guid.NewGuid(), PlatformNotificationId = id, TenantId = tenantId });
            await context.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }
}
