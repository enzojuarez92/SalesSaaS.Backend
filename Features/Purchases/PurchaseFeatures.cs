using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Purchases;

public sealed record CreateSupplierCommand(Guid TenantId, string LegalName, string TaxId, string TaxCondition, string? Email) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetSuppliersQuery(Guid TenantId) : IRequest<IReadOnlyList<SupplierDto>>, ITenantScopedRequest;
public sealed record GetSupplierAccountQuery(Guid TenantId, Guid SupplierId) : IRequest<IReadOnlyList<SupplierAccountEntryDto>>, ITenantScopedRequest;
public sealed record SupplierDto(Guid Id, string LegalName, string TaxId, string TaxCondition, string? Email, bool IsActive);
public sealed record SupplierAccountEntryDto(Guid Id, Guid? PurchaseInvoiceId, decimal Amount, bool IsDebit, string Description, DateTime OccurredAtUtc);
public sealed record PurchaseOrderItemRequest(Guid ProductId, int Quantity, decimal UnitCost);
public sealed record CreatePurchaseOrderCommand(Guid TenantId, Guid SupplierId, Guid WarehouseId, List<PurchaseOrderItemRequest> Items) : IRequest<Guid>, ITenantScopedRequest;
public sealed record ReceivePurchaseOrderCommand(Guid TenantId, Guid PurchaseOrderId) : IRequest, ITenantScopedRequest;
public sealed record CreatePurchaseInvoiceCommand(Guid TenantId, Guid PurchaseOrderId, string Number) : IRequest<Guid>, ITenantScopedRequest;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.LegalName).NotEmpty().MaximumLength(150).WithMessage("La razón social es obligatoria y no puede superar los 150 caracteres.");
        RuleFor(command => command.TaxId).NotEmpty().MaximumLength(20).WithMessage("El CUIT es obligatorio y no puede superar los 20 caracteres.");
        RuleFor(command => command.TaxCondition).NotEmpty().MaximumLength(80).WithMessage("La condición frente al IVA es obligatoria.");
        RuleFor(command => command.Email).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.Email)).WithMessage("El correo electrónico no tiene un formato válido.");
    }
}

public sealed class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.SupplierId).NotEmpty().WithMessage("El proveedor es obligatorio.");
        RuleFor(command => command.WarehouseId).NotEmpty().WithMessage("El depósito es obligatorio.");
        RuleFor(command => command.Items).NotEmpty().WithMessage("La orden de compra debe incluir al menos un producto.");
        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(line => line.ProductId).NotEmpty().WithMessage("El producto es obligatorio.");
            item.RuleFor(line => line.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
            item.RuleFor(line => line.UnitCost).GreaterThanOrEqualTo(0).WithMessage("El costo unitario no puede ser negativo.");
        });
    }
}

public sealed class CreatePurchaseInvoiceCommandValidator : AbstractValidator<CreatePurchaseInvoiceCommand>
{
    public CreatePurchaseInvoiceCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.PurchaseOrderId).NotEmpty().WithMessage("La orden de compra es obligatoria.");
        RuleFor(command => command.Number).NotEmpty().MaximumLength(50).WithMessage("El número de factura es obligatorio y no puede superar los 50 caracteres.");
    }
}

public sealed class CreateSupplierCommandHandler(ApplicationDbContext context) : IRequestHandler<CreateSupplierCommand, Guid>
{
    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var taxId = request.TaxId.Trim();
        if (await context.Suppliers.AnyAsync(item => item.TenantId == request.TenantId && item.TaxId == taxId, cancellationToken))
            throw new InvalidOperationException("Ya existe un proveedor con ese CUIT.");

        var supplier = new Supplier { Id = Guid.NewGuid(), TenantId = request.TenantId, LegalName = request.LegalName.Trim(), TaxId = taxId, TaxCondition = request.TaxCondition.Trim(), Email = request.Email?.Trim() };
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync(cancellationToken);
        return supplier.Id;
    }
}

public sealed class GetSuppliersQueryHandler(ApplicationDbContext context) : IRequestHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    public async Task<IReadOnlyList<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken) =>
        await context.Suppliers.AsNoTracking().Where(item => item.TenantId == request.TenantId).OrderBy(item => item.LegalName)
            .Select(item => new SupplierDto(item.Id, item.LegalName, item.TaxId, item.TaxCondition, item.Email, item.IsActive)).ToListAsync(cancellationToken);
}

public sealed class GetSupplierAccountQueryHandler(ApplicationDbContext context) : IRequestHandler<GetSupplierAccountQuery, IReadOnlyList<SupplierAccountEntryDto>>
{
    public async Task<IReadOnlyList<SupplierAccountEntryDto>> Handle(GetSupplierAccountQuery request, CancellationToken cancellationToken) =>
        await context.SupplierAccountEntries.AsNoTracking().Where(item => item.TenantId == request.TenantId && item.SupplierId == request.SupplierId).OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => new SupplierAccountEntryDto(item.Id, item.PurchaseInvoiceId, item.Amount, item.IsDebit, item.Description, item.OccurredAtUtc)).ToListAsync(cancellationToken);
}

public sealed class CreatePurchaseOrderCommandHandler(ApplicationDbContext context) : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        if (!await context.Suppliers.AnyAsync(item => item.Id == request.SupplierId && item.TenantId == request.TenantId && item.IsActive, cancellationToken)) throw new InvalidOperationException("El proveedor no existe o no está activo.");
        if (!await context.Warehouses.AnyAsync(item => item.Id == request.WarehouseId && item.TenantId == request.TenantId && item.IsActive, cancellationToken)) throw new InvalidOperationException("El depósito no existe o no está activo.");
        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var productCount = await context.Products.CountAsync(item => item.TenantId == request.TenantId && item.IsActive && productIds.Contains(item.Id), cancellationToken);
        if (productCount != productIds.Count) throw new InvalidOperationException("Uno o más productos no existen o no están activos.");

        var order = new PurchaseOrder { Id = Guid.NewGuid(), TenantId = request.TenantId, SupplierId = request.SupplierId, WarehouseId = request.WarehouseId };
        foreach (var line in request.Items)
        {
            var total = line.Quantity * line.UnitCost;
            order.TotalAmount += total;
            order.Items.Add(new PurchaseOrderItem { Id = Guid.NewGuid(), TenantId = request.TenantId, PurchaseOrderId = order.Id, ProductId = line.ProductId, Quantity = line.Quantity, UnitCost = line.UnitCost, TotalAmount = total });
        }
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync(cancellationToken);
        return order.Id;
    }
}

public sealed class ReceivePurchaseOrderCommandHandler(ApplicationDbContext context) : IRequestHandler<ReceivePurchaseOrderCommand>
{
    public async Task Handle(ReceivePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await context.PurchaseOrders.Include(item => item.Items).SingleOrDefaultAsync(item => item.Id == request.PurchaseOrderId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("La orden de compra no existe.");
        if (order.Status != "Draft") throw new InvalidOperationException("La orden de compra no se encuentra pendiente de recepción.");

        var productIds = order.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await context.Products.Where(item => item.TenantId == request.TenantId && item.IsActive && productIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        if (products.Count != productIds.Count) throw new InvalidOperationException("Uno o más productos de la orden no están disponibles.");

        foreach (var line in order.Items)
        {
            var product = products[line.ProductId];
            product.Stock += line.Quantity;
            context.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, ProductId = product.Id, WarehouseId = order.WarehouseId, Type = StockMovementType.Receipt, Quantity = line.Quantity, Reason = "Recepción de compra", Reference = order.Id.ToString("N") });
        }
        order.Status = "Received";
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CreatePurchaseInvoiceCommandHandler(ApplicationDbContext context) : IRequestHandler<CreatePurchaseInvoiceCommand, Guid>
{
    public async Task<Guid> Handle(CreatePurchaseInvoiceCommand request, CancellationToken cancellationToken)
    {
        var order = await context.PurchaseOrders.SingleOrDefaultAsync(item => item.Id == request.PurchaseOrderId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("La orden de compra no existe.");
        if (order.Status != "Received") throw new InvalidOperationException("La orden de compra debe estar recibida antes de registrar la factura.");
        if (await context.PurchaseInvoices.AnyAsync(item => item.PurchaseOrderId == order.Id, cancellationToken)) throw new InvalidOperationException("La orden de compra ya posee una factura registrada.");
        var number = request.Number.Trim();
        if (await context.PurchaseInvoices.AnyAsync(item => item.TenantId == request.TenantId && item.Number == number, cancellationToken)) throw new InvalidOperationException("Ya existe una factura de compra con ese número.");

        var invoice = new PurchaseInvoice { Id = Guid.NewGuid(), TenantId = request.TenantId, PurchaseOrderId = order.Id, SupplierId = order.SupplierId, Number = number, TotalAmount = order.TotalAmount };
        context.PurchaseInvoices.Add(invoice);
        context.SupplierAccountEntries.Add(new SupplierAccountEntry { Id = Guid.NewGuid(), TenantId = request.TenantId, SupplierId = order.SupplierId, PurchaseInvoiceId = invoice.Id, Amount = order.TotalAmount, IsDebit = false, Description = $"Factura de compra {number}" });
        await context.SaveChangesAsync(cancellationToken);
        return invoice.Id;
    }
}
