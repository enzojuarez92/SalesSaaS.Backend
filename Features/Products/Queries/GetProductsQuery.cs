using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Products.Queries;

public record GetProductsQuery(Guid TenantId) : IRequest<List<ProductDto>>;

public record ProductDto(
    Guid Id,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    int Stock
);

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, List<ProductDto>>
{
    private readonly ApplicationDbContext _context;

    public GetProductsQueryHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .Where(p => p.TenantId == request.TenantId)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.Description,
                p.Price,
                p.Stock
            ))
            .ToListAsync(cancellationToken);
    }
}