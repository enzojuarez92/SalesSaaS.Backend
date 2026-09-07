using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.CashRegisters;

public sealed record OpenCashRegisterSessionCommand(Guid TenantId, Guid WarehouseId, decimal OpeningBalance) : IRequest<Guid>, ITenantScopedRequest;
public sealed record RecordCashMovementCommand(Guid TenantId, Guid CashRegisterSessionId, PaymentMethod PaymentMethod, decimal Amount, bool IsIncome, string Description) : IRequest<Guid>, ITenantScopedRequest;
public sealed record CloseCashRegisterSessionCommand(Guid TenantId, Guid CashRegisterSessionId, decimal ClosingBalance) : IRequest, ITenantScopedRequest;

public sealed class OpenCashRegisterSessionCommandValidator : AbstractValidator<OpenCashRegisterSessionCommand>
{
    public OpenCashRegisterSessionCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.WarehouseId).NotEmpty().WithMessage("El depósito es obligatorio.");
        RuleFor(command => command.OpeningBalance).GreaterThanOrEqualTo(0).WithMessage("El saldo inicial no puede ser negativo.");
    }
}

public sealed class RecordCashMovementCommandValidator : AbstractValidator<RecordCashMovementCommand>
{
    public RecordCashMovementCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.CashRegisterSessionId).NotEmpty().WithMessage("La sesión de caja es obligatoria.");
        RuleFor(command => command.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor a cero.");
        RuleFor(command => command.Description).NotEmpty().MaximumLength(300).WithMessage("La descripción es obligatoria y no puede superar los 300 caracteres.");
    }
}

public sealed class CloseCashRegisterSessionCommandValidator : AbstractValidator<CloseCashRegisterSessionCommand>
{
    public CloseCashRegisterSessionCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.CashRegisterSessionId).NotEmpty().WithMessage("La sesión de caja es obligatoria.");
        RuleFor(command => command.ClosingBalance).GreaterThanOrEqualTo(0).WithMessage("El saldo de cierre no puede ser negativo.");
    }
}

public sealed class OpenCashRegisterSessionCommandHandler(ApplicationDbContext context) : IRequestHandler<OpenCashRegisterSessionCommand, Guid>
{
    public async Task<Guid> Handle(OpenCashRegisterSessionCommand request, CancellationToken cancellationToken)
    {
        if (!await context.Warehouses.AnyAsync(item => item.Id == request.WarehouseId && item.TenantId == request.TenantId && item.IsActive, cancellationToken)) throw new InvalidOperationException("El depósito no existe o no está activo.");
        if (await context.CashRegisterSessions.AnyAsync(item => item.TenantId == request.TenantId && item.WarehouseId == request.WarehouseId && item.Status == "Open", cancellationToken)) throw new InvalidOperationException("Ya existe una sesión de caja abierta para este depósito.");
        var session = new CashRegisterSession { Id = Guid.NewGuid(), TenantId = request.TenantId, WarehouseId = request.WarehouseId, OpeningBalance = request.OpeningBalance };
        context.CashRegisterSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        return session.Id;
    }
}

public sealed class RecordCashMovementCommandHandler(ApplicationDbContext context) : IRequestHandler<RecordCashMovementCommand, Guid>
{
    public async Task<Guid> Handle(RecordCashMovementCommand request, CancellationToken cancellationToken)
    {
        var session = await context.CashRegisterSessions.SingleOrDefaultAsync(item => item.Id == request.CashRegisterSessionId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("La sesión de caja no existe.");
        if (session.Status != "Open") throw new InvalidOperationException("No se pueden registrar movimientos en una caja cerrada.");
        var movement = new CashMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, CashRegisterSessionId = session.Id, PaymentMethod = request.PaymentMethod, Amount = request.Amount, IsIncome = request.IsIncome, Description = request.Description.Trim() };
        context.CashMovements.Add(movement);
        await context.SaveChangesAsync(cancellationToken);
        return movement.Id;
    }
}

public sealed class CloseCashRegisterSessionCommandHandler(ApplicationDbContext context) : IRequestHandler<CloseCashRegisterSessionCommand>
{
    public async Task Handle(CloseCashRegisterSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await context.CashRegisterSessions.SingleOrDefaultAsync(item => item.Id == request.CashRegisterSessionId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("La sesión de caja no existe.");
        if (session.Status != "Open") throw new InvalidOperationException("La sesión de caja ya está cerrada.");
        session.ClosingBalance = request.ClosingBalance;
        session.ClosedAtUtc = DateTime.UtcNow;
        session.Status = "Closed";
        await context.SaveChangesAsync(cancellationToken);
    }
}
