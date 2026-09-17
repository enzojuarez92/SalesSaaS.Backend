using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record SupportTicketDto(Guid Id, Guid TenantId, string? TenantName, string? UserName, string Subject, string Message, string? Response, SupportTicketStatus Status, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record CreateSupportTicketRequest(string Subject, string Message);
public sealed record ReplySupportTicketRequest(string Response, SupportTicketStatus Status = SupportTicketStatus.Resolved);

[ApiController]
[Route("api/support")]
[Authorize]
public sealed class SupportController(ApplicationDbContext context, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SupportTicketDto>> Get(CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar el negocio activo.");
        return await context.SupportTickets.AsNoTracking().Where(ticket => ticket.TenantId == currentUser.TenantId.Value).OrderByDescending(ticket => ticket.UpdatedAtUtc)
            .Select(ticket => new SupportTicketDto(ticket.Id, ticket.TenantId, null, null, ticket.Subject, ticket.Message, ticket.Response, ticket.Status, ticket.CreatedAtUtc, ticket.UpdatedAtUtc)).ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<SupportTicketDto>> Create([FromBody] CreateSupportTicketRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue) throw new UnauthorizedAccessException("No se pudo identificar al usuario actual.");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length > 200) return BadRequest(new { message = "El asunto es obligatorio y no puede superar los 200 caracteres." });
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 4000) return BadRequest(new { message = "El mensaje es obligatorio y no puede superar los 4000 caracteres." });
        var ticket = new SupportTicket { Id = Guid.NewGuid(), TenantId = currentUser.TenantId.Value, UserId = currentUser.UserId.Value, Subject = request.Subject.Trim(), Message = request.Message.Trim() };
        context.SupportTickets.Add(ticket); await context.SaveChangesAsync(cancellationToken);
        return Created($"/api/support/{ticket.Id}", new SupportTicketDto(ticket.Id, ticket.TenantId, null, null, ticket.Subject, ticket.Message, null, ticket.Status, ticket.CreatedAtUtc, ticket.UpdatedAtUtc));
    }
}

[ApiController]
[Route("api/superadmin/support")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class SuperAdminSupportController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SupportTicketDto>> Get(CancellationToken cancellationToken) =>
        await (from ticket in context.SupportTickets.IgnoreQueryFilters().AsNoTracking()
               join tenant in context.Tenants.IgnoreQueryFilters().AsNoTracking() on ticket.TenantId equals tenant.Id
               join user in context.Users.AsNoTracking() on ticket.UserId equals user.Id
               orderby ticket.UpdatedAtUtc descending
               select new SupportTicketDto(ticket.Id, ticket.TenantId, tenant.Name, user.FirstName + " " + user.LastName, ticket.Subject, ticket.Message, ticket.Response, ticket.Status, ticket.CreatedAtUtc, ticket.UpdatedAtUtc)).ToListAsync(cancellationToken);

    [HttpPut("{id:guid}/reply")]
    public async Task<ActionResult<SupportTicketDto>> Reply(Guid id, [FromBody] ReplySupportTicketRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Response) || request.Response.Trim().Length > 4000) return BadRequest(new { message = "La respuesta es obligatoria y no puede superar los 4000 caracteres." });
        if (request.Status is not (SupportTicketStatus.InProgress or SupportTicketStatus.Resolved)) return BadRequest(new { message = "El estado debe ser En progreso o Resuelto." });
        var ticket = await context.SupportTickets.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (ticket is null) return NotFound();
        ticket.Response = request.Response.Trim(); ticket.Status = request.Status; ticket.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        var tenantName = await context.Tenants.IgnoreQueryFilters().Where(item => item.Id == ticket.TenantId).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken);
        var userName = await context.Users.Where(item => item.Id == ticket.UserId).Select(item => item.FirstName + " " + item.LastName).SingleOrDefaultAsync(cancellationToken);
        return Ok(new SupportTicketDto(ticket.Id, ticket.TenantId, tenantName, userName, ticket.Subject, ticket.Message, ticket.Response, ticket.Status, ticket.CreatedAtUtc, ticket.UpdatedAtUtc));
    }
}
