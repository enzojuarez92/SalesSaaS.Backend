using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Common;
using SalesSaaS.Infrastructure;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Features.Products.Queries;

public record GetProductsQuery(
    Guid TenantId,
    string? SearchTerm = null,
    bool? IsActive = true,
    Guid? CategoryId = null,
    string? StockStatus = null,
    int PageNumber = 1,
    int PageSize = 10,
    Guid? WarehouseId = null
) : IRequest<PagedResult<ProductDto>>, ITenantScopedRequest;

public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    Guid? CategoryId,
    Guid? BrandId,
    string Description,
    decimal Price,
    decimal Cost,
    decimal VatRate,
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
        var products = _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == request.TenantId);

        // 2. Filtro opcional por estado
        if (request.IsActive.HasValue)
        {
            products = products.Where(p => p.IsActive == request.IsActive.Value);
        }
        if (request.CategoryId.HasValue)
            products = products.Where(product => product.CategoryId == request.CategoryId);

        // 3. Buscador por Código SKU o Nombre
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            products = products.Where(p =>
                p.Sku.ToLower().Contains(term) ||
                p.Name.ToLower().Contains(term));
        }

        // El catálogo pertenece al tenant; la disponibilidad se calcula con los
        // movimientos del depósito seleccionado, nunca con el total del negocio.
        var query = products.Select(product => new
        {
            Product = product,
            Stock = _context.StockMovements.Where(movement => movement.ProductId == product.Id).Sum(movement => (int?)movement.Quantity) ?? 0
        });
        if (!string.IsNullOrWhiteSpace(request.StockStatus))
        {
            var status = request.StockStatus.Trim().ToLowerInvariant();
            query = status switch
            {
                "low" => query.Where(item => item.Stock <= item.Product.MinimumStockAlert),
                "available" => query.Where(item => item.Stock > 0),
                "out" => query.Where(item => item.Stock == 0),
                _ => throw new InvalidOperationException("El estado de stock no es válido.")
            };
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // 5. Paginación y mapeo a DTO
        var items = await query
            .OrderBy(item => item.Product.Name)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => new ProductDto(
                item.Product.Id,
                item.Product.Sku,
                item.Product.Name,
                item.Product.CategoryId,
                item.Product.BrandId,
                item.Product.Description,
                item.Product.Price,
                item.Product.Cost,
                item.Product.VatRate,
                item.Stock,
                item.Product.MinimumStockAlert,
                item.Product.IsActive
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
