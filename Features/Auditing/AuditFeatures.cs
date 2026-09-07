using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Auditing;

public sealed record GetAuditLogsQuery(Guid TenantId, string? EntityName, DateTime? FromUtc, DateTime? ToUtc, int Take = 100) : IRequest<IReadOnlyList<AuditLogDto>>, ITenantScopedRequest;
public sealed record AuditLogDto(Guid Id, Guid? UserId, string EntityName, AuditAction Action, string ChangesJson, DateTime TimestampUtc);

public sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(query => query.EntityName).MaximumLength(200).WithMessage("El nombre de entidad no puede superar los 200 caracteres.");
        RuleFor(query => query.Take).InclusiveBetween(1, 500).WithMessage("La cantidad solicitada debe estar entre 1 y 500.");
        RuleFor(query => query.ToUtc).GreaterThanOrEqualTo(query => query.FromUtc).When(query => query.FromUtc.HasValue && query.ToUtc.HasValue).WithMessage("La fecha final debe ser posterior a la fecha inicial.");
    }
}

public sealed class GetAuditLogsQueryHandler(ApplicationDbContext context) : IRequestHandler<GetAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    public async Task<IReadOnlyList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken) =>
        await context.AuditLogs.AsNoTracking().Where(log => log.TenantId == request.TenantId && (string.IsNullOrWhiteSpace(request.EntityName) || log.EntityName == request.EntityName) && (!request.FromUtc.HasValue || log.TimestampUtc >= request.FromUtc) && (!request.ToUtc.HasValue || log.TimestampUtc <= request.ToUtc))
            .OrderByDescending(log => log.TimestampUtc).Take(request.Take).Select(log => new AuditLogDto(log.Id, log.UserId, log.EntityName, log.Action, log.ChangesJson, log.TimestampUtc)).ToListAsync(cancellationToken);
}
