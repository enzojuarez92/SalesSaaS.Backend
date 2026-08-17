using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Infrastructure;

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
    int MinimumStockAlert
) : IRequest<bool>;

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

        product.Sku = request.Sku;
        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Cost = request.Cost;
        product.Stock = request.Stock;
        product.MinimumStockAlert = request.MinimumStockAlert;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}