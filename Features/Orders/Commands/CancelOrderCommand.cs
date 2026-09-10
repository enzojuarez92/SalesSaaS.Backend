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
            if (order.PaymentMethod == PaymentMethod.Account)
                context.CustomerAccountEntries.Add(new CustomerAccountEntry { Id = Guid.NewGuid(), TenantId = request.TenantId, CustomerId = order.CustomerId, InvoiceId = invoice.Id, Type = CustomerAccountEntryType.Credit, Amount = invoice.TotalAmount, Description = $"Nota de crédito por anulación de factura {invoice.Number}" });
        }
        if (order.PaymentMethod == PaymentMethod.Account)
        {
            var customer = await context.Customers.SingleAsync(item => item.Id == order.CustomerId && item.TenantId == request.TenantId, cancellationToken);
            customer.CurrentBalance = Math.Max(0, customer.CurrentBalance - order.TotalAmount);
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
