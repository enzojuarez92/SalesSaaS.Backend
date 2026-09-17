using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record SupportTicketMessageDto(Guid Id, bool IsFromSupport, string Message, DateTime CreatedAtUtc);
public sealed record SupportTicketDto(Guid Id, Guid TenantId, string? TenantName, string? UserName, string Subject, string Message, string? Response, SupportTicketStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IReadOnlyList<SupportTicketMessageDto> Messages);
public sealed record CreateSupportTicketRequest(string Subject, string Message);
public sealed record AddSupportTicketMessageRequest(string Message);
public sealed record ReplySupportTicketRequest(string Response, SupportTicketStatus Status = SupportTicketStatus.Resolved);

internal static class SupportTicketMapper
{
    public static async Task<Dictionary<Guid, List<SupportTicketMessageDto>>> LoadMessages(ApplicationDbContext context, IEnumerable<Guid> ticketIds, CancellationToken cancellationToken)
    {
        var ids = ticketIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var messages = await context.SupportTicketMessages.AsNoTracking().Where(message => ids.Contains(message.SupportTicketId))
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => new { message.SupportTicketId, Value = new SupportTicketMessageDto(message.Id, message.IsFromSupport, message.Message, message.CreatedAtUtc) })
            .ToListAsync(cancellationToken);
        return messages.GroupBy(message => message.SupportTicketId).ToDictionary(group => group.Key, group => group.Select(message => message.Value).ToList());
    }

    public static SupportTicketDto ToDto(SupportTicket ticket, string? tenantName, string? userName, IReadOnlyList<SupportTicketMessageDto>? messages = null)
    {
        var conversation = new List<SupportTicketMessageDto>();
        if (!string.IsNullOrWhiteSpace(ticket.Response)) conversation.Add(new SupportTicketMessageDto(Guid.Empty, true, ticket.Response, ticket.UpdatedAtUtc));
        if (messages is not null) conversation.AddRange(messages);
        return new SupportTicketDto(ticket.Id, ticket.TenantId, tenantName, userName, ticket.Subject, ticket.Message, ticket.Response, ticket.Status, ticket.CreatedAtUtc, ticket.UpdatedAtUtc, conversation.OrderBy(message => message.CreatedAtUtc).ToList());
    }
}

[ApiController]
[Route("api/support")]
[Authorize]
public sealed class SupportController(ApplicationDbContext context, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SupportTicketDto>> Get(CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar el negocio activo.");
        var tickets = await context.SupportTickets.AsNoTracking().Where(ticket => ticket.TenantId == currentUser.TenantId.Value).OrderByDescending(ticket => ticket.UpdatedAtUtc).ToListAsync(cancellationToken);
        var messages = await SupportTicketMapper.LoadMessages(context, tickets.Select(ticket => ticket.Id), cancellationToken);
        return tickets.Select(ticket => SupportTicketMapper.ToDto(ticket, null, null, messages.GetValueOrDefault(ticket.Id))).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<SupportTicketDto>> Create([FromBody] CreateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar al usuario actual.");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length > 200) return BadRequest(new { message = "El asunto es obligatorio y no puede superar los 200 caracteres." });
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 4000) return BadRequest(new { message = "El mensaje es obligatorio y no puede superar los 4000 caracteres." });
        var ticket = new SupportTicket { Id = Guid.NewGuid(), TenantId = currentUser.TenantId.Value, UserId = currentUser.UserId.Value, Subject = request.Subject.Trim(), Message = request.Message.Trim() };
        context.SupportTickets.Add(ticket); await context.SaveChangesAsync(cancellationToken);
        return Created($"/api/support/{ticket.Id}", SupportTicketMapper.ToDto(ticket, null, null));
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<SupportTicketDto>> AddMessage(Guid id, [FromBody] AddSupportTicketMessageRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar al usuario actual.");
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 4000) return BadRequest(new { message = "El mensaje es obligatorio y no puede superar los 4000 caracteres." });
        var ticket = await context.SupportTickets.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == currentUser.TenantId.Value, cancellationToken);
        if (ticket is null) return NotFound();
        if (ticket.Status == SupportTicketStatus.Resolved) return BadRequest(new { message = "La consulta está resuelta y ya no admite nuevas respuestas." });
        context.SupportTicketMessages.Add(new SupportTicketMessage { Id = Guid.NewGuid(), SupportTicketId = ticket.Id, SenderUserId = currentUser.UserId.Value, Message = request.Message.Trim() });
        ticket.Status = SupportTicketStatus.Open;
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        var messages = await SupportTicketMapper.LoadMessages(context, [ticket.Id], cancellationToken);
        return Ok(SupportTicketMapper.ToDto(ticket, null, null, messages.GetValueOrDefault(ticket.Id)));
    }
}

[ApiController]
[Route("api/superadmin/support")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class SuperAdminSupportController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SupportTicketDto>> Get(CancellationToken cancellationToken)
    {
        var tickets = await context.SupportTickets.IgnoreQueryFilters().AsNoTracking().OrderByDescending(ticket => ticket.UpdatedAtUtc).ToListAsync(cancellationToken);
        var tenantIds = tickets.Select(ticket => ticket.TenantId).Distinct().ToArray();
        var userIds = tickets.Select(ticket => ticket.UserId).Distinct().ToArray();
        var tenantNames = await context.Tenants.IgnoreQueryFilters().AsNoTracking().Where(tenant => tenantIds.Contains(tenant.Id)).ToDictionaryAsync(tenant => tenant.Id, tenant => tenant.Name, cancellationToken);
        var userNames = await context.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.FirstName + " " + user.LastName, cancellationToken);
        var messages = await SupportTicketMapper.LoadMessages(context, tickets.Select(ticket => ticket.Id), cancellationToken);
        return tickets.Select(ticket => SupportTicketMapper.ToDto(ticket, tenantNames.GetValueOrDefault(ticket.TenantId), userNames.GetValueOrDefault(ticket.UserId), messages.GetValueOrDefault(ticket.Id))).ToList();
    }

    [HttpPut("{id:guid}/reply")]
    public async Task<ActionResult<SupportTicketDto>> Reply(Guid id, [FromBody] ReplySupportTicketRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Response) || request.Response.Trim().Length > 4000) return BadRequest(new { message = "La respuesta es obligatoria y no puede superar los 4000 caracteres." });
        if (request.Status is not (SupportTicketStatus.InProgress or SupportTicketStatus.Resolved)) return BadRequest(new { message = "El estado debe ser En progreso o Resuelto." });
        var ticket = await context.SupportTickets.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (ticket is null) return NotFound();
        if (ticket.Status == SupportTicketStatus.Resolved) return BadRequest(new { message = "La consulta ya está resuelta." });
        ticket.Status = request.Status; ticket.UpdatedAtUtc = DateTime.UtcNow;
        context.SupportTicketMessages.Add(new SupportTicketMessage { Id = Guid.NewGuid(), SupportTicketId = ticket.Id, IsFromSupport = true, Message = request.Response.Trim() });
        context.Notifications.Add(new Notification { Id = Guid.NewGuid(), TenantId = ticket.TenantId, UserId = ticket.UserId, Title = "Tenés una respuesta de soporte", Message = $"Respondimos tu consulta: {ticket.Subject}" });
        await context.SaveChangesAsync(cancellationToken);
        var tenantName = await context.Tenants.IgnoreQueryFilters().Where(item => item.Id == ticket.TenantId).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken);
        var userName = await context.Users.Where(item => item.Id == ticket.UserId).Select(item => item.FirstName + " " + item.LastName).SingleOrDefaultAsync(cancellationToken);
        var messages = await SupportTicketMapper.LoadMessages(context, [ticket.Id], cancellationToken);
        return Ok(SupportTicketMapper.ToDto(ticket, tenantName, userName, messages.GetValueOrDefault(ticket.Id)));
    }
}
