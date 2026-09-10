using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.CashRegisters;

public sealed record OpenCashRegisterSessionCommand(Guid TenantId, Guid WarehouseId, decimal OpeningBalance) : IRequest<Guid>, ITenantScopedRequest;
public sealed record RecordCashMovementCommand(Guid TenantId, Guid CashRegisterSessionId, PaymentMethod PaymentMethod, decimal Amount, bool IsIncome, string Description) : IRequest<Guid>, ITenantScopedRequest;
public sealed record CloseCashRegisterSessionCommand(Guid TenantId, Guid CashRegisterSessionId, decimal ClosingBalance) : IRequest<CashCloseResultDto>, ITenantScopedRequest;
public sealed record GetCurrentCashSessionQuery(Guid TenantId, Guid? WarehouseId = null) : IRequest<CashSessionDto?>, ITenantScopedRequest;
public sealed record GetCashSessionHistoryQuery(Guid TenantId, int Take = 20) : IRequest<IReadOnlyList<CashSessionDto>>, ITenantScopedRequest;
public sealed record CashMovementDto(Guid Id, PaymentMethod PaymentMethod, decimal Amount, bool IsIncome, string Description, DateTime OccurredAtUtc);
public sealed record CashPaymentTotalDto(PaymentMethod PaymentMethod, decimal Income, decimal Expense, decimal Net);
public sealed record CashSessionDto(Guid Id, Guid WarehouseId, string WarehouseName, decimal OpeningBalance, decimal ExpectedCash, decimal? ClosingBalance, decimal? Difference, DateTime OpenedAtUtc, DateTime? ClosedAtUtc, string Status, IReadOnlyList<CashPaymentTotalDto> Totals, IReadOnlyList<CashMovementDto> Movements);
public sealed record CashCloseResultDto(Guid Id, decimal ExpectedCash, decimal CountedCash, decimal Difference, DateTime ClosedAtUtc);

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
        RuleFor(command => command.Amount).GreaterThanOrEqualTo(0).WithMessage("El importe no puede ser negativo.");
        RuleFor(command => command.Description).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(300).WithMessage("La descripción es obligatoria y no puede superar los 300 caracteres.");
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

public sealed class CloseCashRegisterSessionCommandHandler(ApplicationDbContext context) : IRequestHandler<CloseCashRegisterSessionCommand, CashCloseResultDto>
{
    public async Task<CashCloseResultDto> Handle(CloseCashRegisterSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await context.CashRegisterSessions.SingleOrDefaultAsync(item => item.Id == request.CashRegisterSessionId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("La sesión de caja no existe.");
        if (session.Status != "Open") throw new InvalidOperationException("La sesión de caja ya está cerrada.");
        var cashNet = await context.CashMovements
            .Where(item => item.CashRegisterSessionId == session.Id && item.PaymentMethod == PaymentMethod.Cash)
            .SumAsync(item => item.IsIncome ? item.Amount : -item.Amount, cancellationToken);
        var expectedCash = session.OpeningBalance + cashNet;
        session.ClosingBalance = request.ClosingBalance;
        session.ClosedAtUtc = DateTime.UtcNow;
        session.Status = "Closed";
        await context.SaveChangesAsync(cancellationToken);
        return new CashCloseResultDto(session.Id, expectedCash, request.ClosingBalance, request.ClosingBalance - expectedCash, session.ClosedAtUtc.Value);
    }
}

public sealed class GetCurrentCashSessionQueryHandler(ApplicationDbContext context) : IRequestHandler<GetCurrentCashSessionQuery, CashSessionDto?>
{
    public async Task<CashSessionDto?> Handle(GetCurrentCashSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await context.CashRegisterSessions.AsNoTracking()
            .Where(item => item.TenantId == request.TenantId && item.Status == "Open" && (!request.WarehouseId.HasValue || item.WarehouseId == request.WarehouseId))
            .OrderByDescending(item => item.OpenedAtUtc).FirstOrDefaultAsync(cancellationToken);
        return session is null ? null : await CashSessionMapper.Map(context, session, cancellationToken);
    }
}

public sealed class GetCashSessionHistoryQueryHandler(ApplicationDbContext context) : IRequestHandler<GetCashSessionHistoryQuery, IReadOnlyList<CashSessionDto>>
{
    public async Task<IReadOnlyList<CashSessionDto>> Handle(GetCashSessionHistoryQuery request, CancellationToken cancellationToken)
    {
        var sessions = await context.CashRegisterSessions.AsNoTracking().Where(item => item.TenantId == request.TenantId)
            .OrderByDescending(item => item.OpenedAtUtc).Take(request.Take).ToListAsync(cancellationToken);
        var result = new List<CashSessionDto>();
        foreach (var session in sessions) result.Add(await CashSessionMapper.Map(context, session, cancellationToken));
        return result;
    }
}

internal static class CashSessionMapper
{
    internal static async Task<CashSessionDto> Map(ApplicationDbContext context, CashRegisterSession session, CancellationToken cancellationToken)
    {
        var warehouseName = await context.Warehouses.AsNoTracking().Where(item => item.Id == session.WarehouseId).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken) ?? "Depósito";
        var movements = await context.CashMovements.AsNoTracking().Where(item => item.CashRegisterSessionId == session.Id).OrderByDescending(item => item.OccurredAtUtc).ToListAsync(cancellationToken);
        var totals = movements.GroupBy(item => item.PaymentMethod).Select(group => new CashPaymentTotalDto(group.Key, group.Where(item => item.IsIncome).Sum(item => item.Amount), group.Where(item => !item.IsIncome).Sum(item => item.Amount), group.Sum(item => item.IsIncome ? item.Amount : -item.Amount))).OrderBy(item => item.PaymentMethod).ToList();
        var expectedCash = session.OpeningBalance + movements.Where(item => item.PaymentMethod == PaymentMethod.Cash).Sum(item => item.IsIncome ? item.Amount : -item.Amount);
        decimal? difference = session.ClosingBalance.HasValue ? session.ClosingBalance.Value - expectedCash : null;
        return new CashSessionDto(session.Id, session.WarehouseId, warehouseName, session.OpeningBalance, expectedCash, session.ClosingBalance, difference, session.OpenedAtUtc, session.ClosedAtUtc, session.Status, totals, movements.Select(item => new CashMovementDto(item.Id, item.PaymentMethod, item.Amount, item.IsIncome, item.Description, item.OccurredAtUtc)).ToList());
    }
}
