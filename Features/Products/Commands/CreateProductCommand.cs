using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Application.Security;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Products.Commands;

public record CreateProductCommand(
    Guid TenantId,
    string Sku,
    string Name,
    string Description,
    decimal Price,
    decimal Cost,
    int Stock,
    int MinimumStockAlert,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    Guid? InitialWarehouseId = null,
    decimal VatRate = 21m
) : IRequest<Guid>, ITenantScopedRequest;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly ApplicationDbContext _context;

    public CreateProductCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Products.AnyAsync(
            p => p.TenantId == request.TenantId && p.Sku == request.Sku,
            cancellationToken
        );

        if (exists)
        {
            throw new InvalidOperationException($"El SKU '{request.Sku}' ya está registrado para este negocio.");
        }

        if (request.CategoryId.HasValue && !await _context.Categories.AnyAsync(category => category.Id == request.CategoryId && category.TenantId == request.TenantId && category.IsActive, cancellationToken))
            throw new InvalidOperationException("La categoría no existe o no está activa.");
        if (request.BrandId.HasValue && !await _context.Brands.AnyAsync(brand => brand.Id == request.BrandId && brand.TenantId == request.TenantId && brand.IsActive, cancellationToken))
            throw new InvalidOperationException("La marca no existe o no está activa.");
        if (request.Stock > 0 && (!request.InitialWarehouseId.HasValue || !await _context.Warehouses.AnyAsync(warehouse => warehouse.Id == request.InitialWarehouseId && warehouse.TenantId == request.TenantId && warehouse.IsActive, cancellationToken)))
            throw new InvalidOperationException("Elegí un depósito activo para asignar el stock inicial.");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            Sku = request.Sku,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Cost = request.Cost,
            VatRate = request.VatRate,
            Stock = request.Stock,
            MinimumStockAlert = request.MinimumStockAlert,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        if (request.Stock > 0)
            _context.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, ProductId = product.Id, WarehouseId = request.InitialWarehouseId!.Value, Type = StockMovementType.Receipt, Quantity = request.Stock, Reason = "Stock inicial", Reference = product.Id.ToString("N") });
        await _context.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
