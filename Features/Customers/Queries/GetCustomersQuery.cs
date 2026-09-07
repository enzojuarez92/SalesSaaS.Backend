using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Common;
using SalesSaaS.Infrastructure;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Features.Customers.Queries;

public record GetCustomersQuery(
    Guid TenantId,
    string? SearchTerm = null,
    bool? IsActive = true, // 👈 Por defecto busca activos
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PagedResult<CustomerDto>>, ITenantScopedRequest;

public record CustomerDto(
    Guid Id,
    string Name,
    string DocumentType,
    string DocumentNumber,
    string TaxCondition,
    string Email,
    string Phone,
    string Address,
    string City,
    string State,
    string PostalCode,
    decimal CreditLimit,
    bool AllowCredit,
    bool IsActive
);

public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, PagedResult<CustomerDto>>
{
    private readonly ApplicationDbContext _context;

    public GetCustomersQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        // 1. AsNoTracking para máxima performance de lectura + Aislamiento de Tenant
        var query = _context.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == request.TenantId);

        // 2. Filtro opcional por estado (IsActive)
        if (request.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == request.IsActive.Value);
        }

        // 3. Buscador multi-campo (Nombre, DNI, Email, Teléfono)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();

            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                c.DocumentNumber.Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)));
        }

        // 4. Conteo total de coincidencias para la paginación
        var totalCount = await query.CountAsync(cancellationToken);

        // 5. Mapeo a tu CustomerDto + Paginación con Skip/Take
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CustomerDto(
                c.Id,
                c.Name,
                c.DocumentType,
                c.DocumentNumber,
                c.TaxCondition,
                c.Email,
                c.Phone,
                c.Address,
                c.City,
                c.State,
                c.PostalCode,
                c.CreditLimit,
                c.AllowCredit,
                c.IsActive
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
