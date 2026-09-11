using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Features.Orders.Commands;

public record OrderItemRequest(Guid ProductId, int Quantity);

public record CreateOrderCommand(
    Guid TenantId,
    Guid CustomerId,
    Guid WarehouseId,
    List<OrderItemRequest> Items,
    decimal DiscountAmount = 0,
    PaymentMethod PaymentMethod = PaymentMethod.Cash,
    Guid? RequestId = null
) : IRequest<Guid>, ITenantScopedRequest;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly ApplicationDbContext _context;

    public CreateOrderCommandHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { request.CustomerId, request.WarehouseId, request.Items, request.DiscountAmount, request.PaymentMethod })));
        if (request.RequestId.HasValue)
        {
            var existing = await _context.Orders.SingleOrDefaultAsync(o => o.TenantId == request.TenantId && o.RequestId == request.RequestId, cancellationToken);
            if (existing is not null)
            {
                if (existing.RequestFingerprint != fingerprint) throw new InvalidOperationException("La solicitud ya fue procesada con otros datos.");
                return existing.Id;
            }
        }
        if (request.Items == null || !request.Items.Any())
        {
            throw new InvalidOperationException("La venta debe contener al menos un producto.");
        }

        // 1. Validar que el Cliente exista y pertenezca al mismo Tenant
        var customer = await _context.Customers
            .SingleOrDefaultAsync(c => c.Id == request.CustomerId && c.TenantId == request.TenantId && c.IsActive, cancellationToken);

        if (customer is null)
        {
            throw new InvalidOperationException("El cliente especificado no existe o no pertenece a este Inquilino.");
        }

        var warehouseExists = await _context.Warehouses.AnyAsync(warehouse => warehouse.Id == request.WarehouseId && warehouse.TenantId == request.TenantId && warehouse.IsActive, cancellationToken);
        if (!warehouseExists) throw new InvalidOperationException("El depósito no existe o no está activo.");

        // La caja es una condición previa para cualquier venta: se valida antes de
        // modificar stock, saldo del cliente o persistir la orden.
        var activeCashSession = await _context.CashRegisterSessions
            .Where(session => session.TenantId == request.TenantId && session.WarehouseId == request.WarehouseId && session.Status == "Open")
            .OrderByDescending(session => session.OpenedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeCashSession is null)
            throw new InvalidOperationException("No se puede procesar la venta porque no hay una caja abierta para hoy.");

        // 2. Cargar los productos de la BD para verificar precios y stock
        var productIds = request.Items.Select(i => i.ProductId).ToList();
        var products = await _context.Products
            .Where(p => p.TenantId == request.TenantId && p.IsActive && productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
        var availableByProduct = await _context.StockMovements
            .Where(movement => movement.TenantId == request.TenantId && movement.WarehouseId == request.WarehouseId && productIds.Contains(movement.ProductId))
            .GroupBy(movement => movement.ProductId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(movement => movement.Quantity), cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            RequestId = request.RequestId,
            RequestFingerprint = fingerprint,
            TenantId = request.TenantId,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
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

            var available = availableByProduct.GetValueOrDefault(product.Id);
            if (available < itemRequest.Quantity)
            {
                throw new InvalidOperationException($"Stock insuficiente en el depósito seleccionado para el producto '{product.Name}'. Stock actual: {available}, solicitado: {itemRequest.Quantity}.");
            }

            availableByProduct[product.Id] = available - itemRequest.Quantity;
            // Product.Stock conserva el total del tenant; el saldo operativo se
            // controla con movimientos del depósito.
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
            _context.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, ProductId = product.Id, WarehouseId = request.WarehouseId, Type = StockMovementType.Issue, Quantity = -itemRequest.Quantity, Reference = order.Id.ToString("N"), Reason = "Venta confirmada" });
        }

        if (request.DiscountAmount > totalAmount)
        {
            throw new InvalidOperationException("El descuento no puede superar el subtotal de la venta.");
        }

        order.DiscountAmount = request.DiscountAmount;
        order.TotalAmount = totalAmount - request.DiscountAmount;
        order.PaymentMethod = request.PaymentMethod;
        if (request.PaymentMethod == PaymentMethod.Account)
        {
            if (!customer.AllowCredit)
                throw new InvalidOperationException("El cliente no tiene cuenta corriente habilitada.");
            if (customer.CurrentBalance + order.TotalAmount > customer.CreditLimit)
                throw new InvalidOperationException("La venta supera el crédito disponible del cliente.");
            customer.CurrentBalance += order.TotalAmount;
            _context.CustomerAccountEntries.Add(new CustomerAccountEntry { Id = Guid.NewGuid(), TenantId = request.TenantId, WarehouseId = request.WarehouseId, CustomerId = customer.Id, Type = CustomerAccountEntryType.Debit, Amount = order.TotalAmount, Description = $"Venta {order.Id:N}" });
        }

        // Una venta cobrada se registra en la caja abierta del mismo depósito. Las
        // ventas a cuenta corriente no representan un ingreso de dinero todavía.
        if (request.PaymentMethod != PaymentMethod.Account)
        {
            _context.CashMovements.Add(new CashMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, CashRegisterSessionId = activeCashSession.Id, PaymentMethod = request.PaymentMethod, Amount = order.TotalAmount, IsIncome = true, Description = $"Venta {order.Id:N}" });
        }

        // 4. Guardar Venta y cambios de Stock en una sola transacción
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}
