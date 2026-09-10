using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Application.Billing;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Inventory.Warehouses;

public sealed record CreateWarehouseCommand(Guid TenantId, string Code, string Name, string? Address) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetWarehousesQuery(Guid TenantId) : IRequest<IReadOnlyList<WarehouseDto>>, ITenantScopedRequest;
public sealed record UpdateWarehouseCommand(Guid Id, Guid TenantId, string Name, string? Address, bool IsActive) : IRequest<WarehouseDto>, ITenantScopedRequest;
public sealed record WarehouseDto(Guid Id, string Code, string Name, string? Address, bool IsActive);

public sealed class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.Code).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(30).WithMessage("El código del depósito es obligatorio y no puede superar los 30 caracteres.");
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100).WithMessage("El nombre del depósito es obligatorio y no puede superar los 100 caracteres.");
        RuleFor(command => command.Address).MaximumLength(250).WithMessage("La dirección no puede superar los 250 caracteres.");
    }
}
public sealed class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100).WithMessage("El nombre del depósito es obligatorio y no puede superar los 100 caracteres.");
        RuleFor(command => command.Address).MaximumLength(250).WithMessage("La dirección no puede superar los 250 caracteres.");
    }
}

public sealed class CreateWarehouseCommandHandler(ApplicationDbContext context, ISubscriptionGatekeeper subscriptionGatekeeper) : IRequestHandler<CreateWarehouseCommand, Guid>
{
    public async Task<Guid> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        await subscriptionGatekeeper.EnsureCanAddWarehouseAsync(request.TenantId, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        if (await context.Warehouses.AnyAsync(warehouse => warehouse.TenantId == request.TenantId && warehouse.Code == code, cancellationToken))
            throw new InvalidOperationException("Ya existe un depósito con ese código.");
        var warehouse = new Warehouse { Id = Guid.NewGuid(), TenantId = request.TenantId, Code = code, Name = request.Name.Trim(), Address = request.Address?.Trim() };
        context.Warehouses.Add(warehouse);
        await context.SaveChangesAsync(cancellationToken);
        return warehouse.Id;
    }
}

public sealed class GetWarehousesQueryHandler(ApplicationDbContext context) : IRequestHandler<GetWarehousesQuery, IReadOnlyList<WarehouseDto>>
{
    public async Task<IReadOnlyList<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken) =>
        await context.Warehouses.AsNoTracking().Where(warehouse => warehouse.TenantId == request.TenantId).OrderBy(warehouse => warehouse.Name)
            .Select(warehouse => new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive)).ToListAsync(cancellationToken);
}

public sealed class UpdateWarehouseCommandHandler(ApplicationDbContext context) : IRequestHandler<UpdateWarehouseCommand, WarehouseDto>
{
    public async Task<WarehouseDto> Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await context.Warehouses.SingleOrDefaultAsync(item => item.Id == request.Id && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("El depósito no existe.");
        if (!request.IsActive && warehouse.IsActive)
        {
            var anotherActive = await context.Warehouses.AnyAsync(item => item.TenantId == request.TenantId && item.Id != warehouse.Id && item.IsActive, cancellationToken);
            if (!anotherActive) throw new InvalidOperationException("El negocio debe conservar al menos un depósito activo.");
        }
        warehouse.Name = request.Name.Trim();
        warehouse.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        warehouse.IsActive = request.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        return new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive);
    }
}
