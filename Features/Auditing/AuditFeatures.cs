using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using System.Text.Json;

namespace SalesSaaS.Features.Auditing;

public sealed record GetAuditLogsQuery(Guid TenantId, string? EntityName, DateTime? FromUtc, DateTime? ToUtc, int Take = 100, Guid? WarehouseId = null) : IRequest<IReadOnlyList<AuditLogDto>>, ITenantScopedRequest;
public sealed record AuditLogDto(Guid Id, Guid? UserId, string? UserName, Guid? WarehouseId, string EntityName, AuditAction Action, string ChangesJson, DateTime TimestampUtc);

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
    private static readonly HashSet<string> VisibleChangeFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Name", "LegalName", "Description", "Price", "Cost", "Stock", "MinimumStockAlert", "Quantity", "Status",
        "PaymentMethod", "TotalAmount", "IsActive", "Email", "Phone", "Address", "TaxId", "Code", "Reason", "Reference"
    };

    public async Task<IReadOnlyList<AuditLogDto>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var logs = await (
            from log in context.AuditLogs.AsNoTracking()
            join user in context.Users.AsNoTracking() on log.UserId equals (Guid?)user.Id into users
            from user in users.DefaultIfEmpty()
            where log.TenantId == request.TenantId
                && (!request.WarehouseId.HasValue || log.WarehouseId == request.WarehouseId)
                && (string.IsNullOrWhiteSpace(request.EntityName) || log.EntityName == request.EntityName)
                && (!request.FromUtc.HasValue || log.TimestampUtc >= request.FromUtc)
                && (!request.ToUtc.HasValue || log.TimestampUtc <= request.ToUtc)
            orderby log.TimestampUtc descending
            select new
            {
                log.Id,
                log.UserId,
                UserName = user == null ? null : user.FirstName + " " + user.LastName,
                log.WarehouseId,
                log.EntityName,
                log.Action,
                log.ChangesJson,
                log.TimestampUtc
            }
        ).Take(request.Take).ToListAsync(cancellationToken);

        return logs.Select(log => new AuditLogDto(
            log.Id,
            log.UserId,
            log.UserName,
            log.WarehouseId,
            log.EntityName,
            log.Action,
            SanitizeChangesJson(log.ChangesJson),
            log.TimestampUtc)).ToList();
    }

    private static string SanitizeChangesJson(string changesJson)
    {
        try
        {
            using var document = JsonDocument.Parse(changesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return "{}";

            var visibleChanges = document.RootElement.EnumerateObject()
                .Where(property => VisibleChangeFields.Contains(property.Name))
                .ToDictionary(property => property.Name, property => property.Value.Clone());

            return JsonSerializer.Serialize(visibleChanges);
        }
        catch (JsonException)
        {
            return "{}";
        }
    }
}
