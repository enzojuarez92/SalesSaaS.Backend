using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using SalesSaaS.Features.Notifications;

namespace SalesSaaS.Features.Inventory.Stock;

public sealed record RecordStockMovementCommand(Guid TenantId, Guid ProductId, Guid WarehouseId, StockMovementType Type, int Quantity, string? Reason, string? Reference) : IRequest<Guid>, ITenantScopedRequest;
public sealed record TransferStockCommand(Guid TenantId, Guid ProductId, Guid SourceWarehouseId, Guid DestinationWarehouseId, int Quantity, string? Reference) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetStockMovementsQuery(Guid TenantId, Guid ProductId, Guid? WarehouseId = null) : IRequest<IReadOnlyList<StockMovementDto>>, ITenantScopedRequest;
public sealed record StockMovementDto(Guid Id, Guid ProductId, Guid WarehouseId, StockMovementType Type, int Quantity, string? Reason, string? Reference, DateTime OccurredAtUtc);

public sealed class RecordStockMovementCommandValidator : AbstractValidator<RecordStockMovementCommand>
{
    public RecordStockMovementCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.ProductId).NotEmpty().WithMessage("El producto es obligatorio.");
        RuleFor(command => command.WarehouseId).NotEmpty().WithMessage("El depósito es obligatorio.");
        RuleFor(command => command.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
        RuleFor(command => command.Reason).NotEmpty().When(command => command.Type is StockMovementType.AdjustmentIncrease or StockMovementType.AdjustmentDecrease).WithMessage("El motivo es obligatorio para ajustes de stock.");
    }
}

public sealed class TransferStockCommandValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.ProductId).NotEmpty().WithMessage("El producto es obligatorio.");
        RuleFor(command => command.SourceWarehouseId).NotEmpty().WithMessage("El depósito de origen es obligatorio.");
        RuleFor(command => command.DestinationWarehouseId).NotEmpty().WithMessage("El depósito de destino es obligatorio.");
        RuleFor(command => command.DestinationWarehouseId).NotEqual(command => command.SourceWarehouseId).WithMessage("Los depósitos de origen y destino deben ser distintos.");
        RuleFor(command => command.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
    }
}

public sealed class RecordStockMovementCommandHandler(ApplicationDbContext context, IPublisher publisher) : IRequestHandler<RecordStockMovementCommand, Guid>
{
    public async Task<Guid> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        var product = await context.Products.SingleOrDefaultAsync(item => item.Id == request.ProductId && item.TenantId == request.TenantId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("El producto no existe o no está activo.");
        var warehouseExists = await context.Warehouses.AnyAsync(item => item.Id == request.WarehouseId && item.TenantId == request.TenantId && item.IsActive, cancellationToken);
        if (!warehouseExists) throw new InvalidOperationException("El depósito no existe o no está activo.");

        var delta = GetDelta(request.Type, request.Quantity);
        var warehouseStock = await context.StockMovements.Where(item => item.ProductId == request.ProductId && item.WarehouseId == request.WarehouseId).SumAsync(item => (int?)item.Quantity, cancellationToken) ?? 0;
        if (delta < 0 && warehouseStock + delta < 0) throw new InvalidOperationException("El depósito seleccionado no tiene stock suficiente para registrar la salida.");

        var previousStock = product.Stock;
        product.Stock += delta;
        var movement = new StockMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, ProductId = request.ProductId, WarehouseId = request.WarehouseId, Type = request.Type, Quantity = delta, Reason = request.Reason?.Trim(), Reference = request.Reference?.Trim() };
        context.StockMovements.Add(movement);
        await context.SaveChangesAsync(cancellationToken);
        if (previousStock > product.MinimumStockAlert && product.Stock <= product.MinimumStockAlert) await publisher.Publish(new LowStockReachedEvent(request.TenantId, product.Id, product.Name, product.Stock), cancellationToken);
        return movement.Id;
    }

    internal static int GetDelta(StockMovementType type, int quantity) => type switch
    {
        StockMovementType.Receipt or StockMovementType.AdjustmentIncrease or StockMovementType.TransferIn => quantity,
        StockMovementType.Issue or StockMovementType.AdjustmentDecrease or StockMovementType.TransferOut => -quantity,
        _ => throw new InvalidOperationException("El tipo de movimiento de stock no es válido.")
    };
}

public sealed class TransferStockCommandHandler(ApplicationDbContext context) : IRequestHandler<TransferStockCommand, Guid>
{
    public async Task<Guid> Handle(TransferStockCommand request, CancellationToken cancellationToken)
    {
        var product = await context.Products.SingleOrDefaultAsync(item => item.Id == request.ProductId && item.TenantId == request.TenantId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("El producto no existe o no está activo.");
        var warehouseCount = await context.Warehouses.CountAsync(item => item.TenantId == request.TenantId && item.IsActive && (item.Id == request.SourceWarehouseId || item.Id == request.DestinationWarehouseId), cancellationToken);
        if (warehouseCount != 2) throw new InvalidOperationException("Uno o ambos depósitos no existen o no están activos.");
        var sourceBalance = await context.StockMovements.Where(item => item.ProductId == product.Id && item.WarehouseId == request.SourceWarehouseId).SumAsync(item => (int?)item.Quantity, cancellationToken) ?? 0;
        if (sourceBalance < request.Quantity) throw new InvalidOperationException("El depósito de origen no tiene stock suficiente para la transferencia.");

        var reference = request.Reference?.Trim();
        var transferId = Guid.NewGuid().ToString("N");
        context.StockMovements.AddRange(
            new StockMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, ProductId = product.Id, WarehouseId = request.SourceWarehouseId, Type = StockMovementType.TransferOut, Quantity = -request.Quantity, Reference = reference ?? transferId },
            new StockMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, ProductId = product.Id, WarehouseId = request.DestinationWarehouseId, Type = StockMovementType.TransferIn, Quantity = request.Quantity, Reference = reference ?? transferId });
        await context.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}

public sealed class GetStockMovementsQueryHandler(ApplicationDbContext context) : IRequestHandler<GetStockMovementsQuery, IReadOnlyList<StockMovementDto>>
{
    public async Task<IReadOnlyList<StockMovementDto>> Handle(GetStockMovementsQuery request, CancellationToken cancellationToken) =>
        await context.StockMovements.AsNoTracking().Where(item => item.TenantId == request.TenantId && item.ProductId == request.ProductId && (!request.WarehouseId.HasValue || item.WarehouseId == request.WarehouseId))
            .OrderByDescending(item => item.OccurredAtUtc).Select(item => new StockMovementDto(item.Id, item.ProductId, item.WarehouseId, item.Type, item.Quantity, item.Reason, item.Reference, item.OccurredAtUtc)).ToListAsync(cancellationToken);
}
