using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Orders.Commands;

public record OrderItemRequest(Guid ProductId, int Quantity);

public record CreateOrderCommand(
    Guid TenantId,
    Guid CustomerId,
    List<OrderItemRequest> Items
) : IRequest<Guid>;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly ApplicationDbContext _context;

    public CreateOrderCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
        {
            throw new InvalidOperationException("La venta debe contener al menos un producto.");
        }

        // 1. Validar que el Cliente exista y pertenezca al mismo Tenant
        var customerExists = await _context.Customers
            .AnyAsync(c => c.Id == request.CustomerId && c.TenantId == request.TenantId, cancellationToken);

        if (!customerExists)
        {
            throw new InvalidOperationException("El cliente especificado no existe o no pertenece a este Inquilino.");
        }

        // 2. Cargar los productos de la BD para verificar precios y stock
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .Where(p => p.TenantId == request.TenantId && productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            CustomerId = request.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };

        decimal totalAmount = 0;

        // 3. Procesar cada ítem, validar stock y calcular subtotal
        foreach (var itemRequest in request.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == itemRequest.ProductId);

            if (product == null)
            {
                throw new InvalidOperationException($"El producto con ID '{itemRequest.ProductId}' no existe.");
            }

            if (product.Stock < itemRequest.Quantity)
            {
                throw new InvalidOperationException($"Stock insuficiente para el producto '{product.Name}'. Stock actual: {product.Stock}, solicitado: {itemRequest.Quantity}.");
            }

            // Descontar stock
            product.Stock -= itemRequest.Quantity;

            var subTotal = product.Price * itemRequest.Quantity;
            totalAmount += subTotal;

            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = itemRequest.Quantity,
                UnitPrice = product.Price,
                SubTotal = subTotal
            });
        }

        order.TotalAmount = totalAmount;

        // 4. Guardar Venta y cambios de Stock en una sola transacción
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}