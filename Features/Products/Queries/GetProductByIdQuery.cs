using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Infrastructure; 

namespace SalesSaaS.Features.Products.Queries;

// 1. La Query: Ahora sí promete retornar un ProductDto (o null si no lo encuentra)
public record GetProductByIdQuery(Guid ProductId, Guid TenantId) : IRequest<ProductDto?>;

// 2. El Handler: Ahora las firmas coinciden a la perfección
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
            .Where(p => p.Id == request.ProductId && p.TenantId == request.TenantId)
            .Select(p => new ProductDto(
                p.Id,
                p.Sku,
                p.Name,
                p.Description,
                p.Price,
                p.Stock
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }
}