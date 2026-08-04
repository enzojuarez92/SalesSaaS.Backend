using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Common;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Products.Queries;

public record GetProductsQuery(
    Guid TenantId,
    string? SearchTerm = null,
    bool? IsActive = true,
    int PageNumber = 1,
    int PageSize = 10
) : IRequest<PagedResult<ProductDto>>;

public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    decimal Cost,
    int Stock,
    int MinimumStockAlert,
    bool IsActive
);

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly ApplicationDbContext _context;

    public GetProductsQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        // 1. Aislamiento por TenantId + AsNoTracking
        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == request.TenantId);

        // 2. Filtro opcional por estado
        if (request.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == request.IsActive.Value);
        }

        // 3. Buscador por Código SKU o Nombre
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.Sku.ToLower().Contains(term) ||
                p.Name.ToLower().Contains(term));
        }

        // 4. Conteo total de coincidencias
        var totalCount = await query.CountAsync(cancellationToken);

        // 5. Paginación y mapeo a DTO
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.Description,
                p.Price,
                p.Cost,
                p.Stock,
                p.MinimumStockAlert,
                p.IsActive
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}