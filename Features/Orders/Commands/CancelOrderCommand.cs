using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Orders.Commands;

public sealed record CancelOrderCommand(Guid TenantId, Guid OrderId, bool ReturnStock, string Reason) : IRequest, ITenantScopedRequest;

public sealed class CancelOrderCommandValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.OrderId).NotEmpty().WithMessage("El pedido es obligatorio.");
        RuleFor(command => command.Reason).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(300).WithMessage("El motivo de cancelación es obligatorio y no puede superar los 300 caracteres.");
    }
}

public sealed class CancelOrderCommandHandler(ApplicationDbContext context) : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await context.Orders.Include(item => item.Items).SingleOrDefaultAsync(item => item.Id == request.OrderId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("El pedido no existe.");
        if (order.Status == "Cancelled") throw new InvalidOperationException("El pedido ya está cancelado.");

        if (await context.Invoices.AnyAsync(i => i.OrderId == order.Id && i.Cae != null, cancellationToken)) throw new InvalidOperationException("La venta tiene autorización fiscal. Emití la nota de crédito antes de anularla.");
        order.Status = "Cancelled";
        if (request.ReturnStock)
        {
            var products = await context.Products.Where(item => order.Items.Select(line => line.ProductId).Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
            foreach (var line in order.Items)
            {
                products[line.ProductId].Stock += line.Quantity;
                context.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, ProductId = line.ProductId, WarehouseId = order.WarehouseId, Type = StockMovementType.Receipt, Quantity = line.Quantity, Reason = request.Reason, Reference = order.Id.ToString("N") });
            }
        }

        var invoices = await context.Invoices.Where(item => item.OrderId == order.Id && item.Status == "Issued").ToListAsync(cancellationToken);
        foreach (var invoice in invoices)
        {
            invoice.Status = "Cancelled";

        }
        if (order.PaymentMethod == PaymentMethod.Account)
        {
            var customer = await context.Customers.SingleAsync(item => item.Id == order.CustomerId && item.TenantId == request.TenantId, cancellationToken);
            customer.CurrentBalance -= order.TotalAmount;
            context.CustomerAccountEntries.Add(new CustomerAccountEntry { Id = Guid.NewGuid(), TenantId = request.TenantId, CustomerId = order.CustomerId, WarehouseId = order.WarehouseId, Type = CustomerAccountEntryType.Credit, Amount = order.TotalAmount, Description = $"Anulación de venta {order.Id:N}" });
        }
        else
        {
            var cash = await context.CashRegisterSessions.SingleOrDefaultAsync(s => s.WarehouseId == order.WarehouseId && s.Status == "Open", cancellationToken)
                ?? throw new InvalidOperationException("Abrí la caja de esta sucursal antes de registrar la devolución.");
            context.CashMovements.Add(new CashMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, CashRegisterSessionId = cash.Id, Amount = order.TotalAmount, PaymentMethod = order.PaymentMethod, IsIncome = false, Description = $"Anulación de venta {order.Id:N}" });
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
