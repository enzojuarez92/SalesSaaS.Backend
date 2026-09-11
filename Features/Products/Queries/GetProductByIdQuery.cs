using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Infrastructure;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Features.Products.Queries;

public record GetProductByIdQuery(Guid ProductId, Guid TenantId) : IRequest<ProductDto?>, ITenantScopedRequest;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly ApplicationDbContext _context;

    public GetProductByIdQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.Id == request.ProductId && p.TenantId == request.TenantId)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.CategoryId,
                p.BrandId,
                p.Description,
                p.Price,
                p.Cost,
                _context.StockMovements.Where(m => m.ProductId == p.Id).Sum(m => (int?)m.Quantity) ?? 0,
                p.MinimumStockAlert,
                p.IsActive
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
