using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Infrastructure;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Features.Products.Commands;

public record UpdateProductCommand(
    Guid Id,
    Guid TenantId,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    decimal Cost,
    int Stock,
    int MinimumStockAlert,
    Guid? CategoryId = null,
    Guid? BrandId = null
) : IRequest<bool>, ITenantScopedRequest;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, bool>
{
    private readonly ApplicationDbContext _context;

    public UpdateProductCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.TenantId == request.TenantId, cancellationToken);

        if (product == null)
        {
            return false;
        }

        var skuBusy = await _context.Products.AnyAsync(
            p => p.TenantId == request.TenantId
              && p.Sku == request.Sku
              && p.Id != request.Id,
            cancellationToken
        );

        if (skuBusy)
        {
            throw new InvalidOperationException($"El SKU '{request.Sku}' ya está siendo utilizado por otro producto.");
        }

        if (request.CategoryId.HasValue && !await _context.Categories.AnyAsync(category => category.Id == request.CategoryId && category.TenantId == request.TenantId && category.IsActive, cancellationToken))
            throw new InvalidOperationException("La categoría no existe o no está activa.");
        if (request.BrandId.HasValue && !await _context.Brands.AnyAsync(brand => brand.Id == request.BrandId && brand.TenantId == request.TenantId && brand.IsActive, cancellationToken))
            throw new InvalidOperationException("La marca no existe o no está activa.");

        product.Sku = request.Sku;
        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Cost = request.Cost;
        product.Stock = request.Stock;
        product.MinimumStockAlert = request.MinimumStockAlert;
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
