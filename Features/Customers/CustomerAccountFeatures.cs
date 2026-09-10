using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Customers;

public sealed record GetCustomerStatementQuery(Guid TenantId, Guid CustomerId, Guid? WarehouseId = null) : IRequest<CustomerStatementDto>, ITenantScopedRequest;
public sealed record CustomerStatementDto(Guid CustomerId, string CustomerName, decimal CurrentBalance, decimal CreditLimit, decimal AvailableCredit, IReadOnlyList<CustomerStatementEntryDto> Entries);
public sealed record CustomerStatementEntryDto(Guid Id, CustomerAccountEntryType Type, decimal Amount, string Description, DateTime OccurredAtUtc, string? InvoiceNumber);
public sealed record RecordCustomerPaymentCommand(Guid TenantId, Guid CustomerId, Guid WarehouseId, decimal Amount, string Description) : IRequest<Guid>, ITenantScopedRequest;

public sealed class GetCustomerStatementQueryHandler(ApplicationDbContext context) : IRequestHandler<GetCustomerStatementQuery, CustomerStatementDto>
{
    public async Task<CustomerStatementDto> Handle(GetCustomerStatementQuery request, CancellationToken cancellationToken)
    {
        var customer = await context.Customers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.CustomerId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("El cliente no existe.");
        var entries = await context.CustomerAccountEntries.AsNoTracking().Where(item => item.TenantId == request.TenantId && item.CustomerId == request.CustomerId && (!request.WarehouseId.HasValue || item.WarehouseId == request.WarehouseId))
            .OrderByDescending(item => item.OccurredAtUtc).Select(item => new CustomerStatementEntryDto(item.Id, item.Type, item.Amount, item.Description, item.OccurredAtUtc, item.Invoice != null ? item.Invoice.Number : null)).ToListAsync(cancellationToken);
        var balance = entries.Sum(item => item.Type == CustomerAccountEntryType.Debit ? item.Amount : -item.Amount);
        return new CustomerStatementDto(customer.Id, customer.Name, balance, customer.CreditLimit, Math.Max(0, customer.CreditLimit - customer.CurrentBalance), entries);
    }
}

public sealed class RecordCustomerPaymentCommandValidator : AbstractValidator<RecordCustomerPaymentCommand>
{
    public RecordCustomerPaymentCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty(); RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.WarehouseId).NotEmpty().WithMessage("El depósito es obligatorio.");
        RuleFor(command => command.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor a cero.");
        RuleFor(command => command.Description).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(300).WithMessage("La descripción es obligatoria y no puede superar los 300 caracteres.");
    }
}

public sealed class RecordCustomerPaymentCommandHandler(ApplicationDbContext context) : IRequestHandler<RecordCustomerPaymentCommand, Guid>
{
    public async Task<Guid> Handle(RecordCustomerPaymentCommand request, CancellationToken cancellationToken)
    {
        var customer = await context.Customers.SingleOrDefaultAsync(item => item.Id == request.CustomerId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("El cliente no existe.");
        if (!await context.Warehouses.AnyAsync(item => item.Id == request.WarehouseId && item.TenantId == request.TenantId && item.IsActive, cancellationToken)) throw new InvalidOperationException("El depósito no existe o no está activo.");
        var warehouseBalance = await context.CustomerAccountEntries.Where(item => item.TenantId == request.TenantId && item.CustomerId == request.CustomerId && item.WarehouseId == request.WarehouseId).SumAsync(item => (decimal?)(item.Type == CustomerAccountEntryType.Debit ? item.Amount : -item.Amount), cancellationToken) ?? 0;
        if (request.Amount > warehouseBalance) throw new InvalidOperationException("El pago no puede superar el saldo pendiente de esta sucursal.");
        var entry = new CustomerAccountEntry { Id = Guid.NewGuid(), TenantId = request.TenantId, CustomerId = request.CustomerId, WarehouseId = request.WarehouseId, Type = CustomerAccountEntryType.Credit, Amount = request.Amount, Description = request.Description.Trim() };
        customer.CurrentBalance -= request.Amount;
        context.CustomerAccountEntries.Add(entry);
        var activeCashSession = await context.CashRegisterSessions
            .Where(session => session.TenantId == request.TenantId && session.WarehouseId == request.WarehouseId && session.Status == "Open")
            .OrderByDescending(session => session.OpenedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (activeCashSession is null) throw new InvalidOperationException("No hay una caja abierta en el depósito seleccionado para registrar el cobro.");
        context.CashMovements.Add(new CashMovement { Id = Guid.NewGuid(), TenantId = request.TenantId, CashRegisterSessionId = activeCashSession.Id, PaymentMethod = PaymentMethod.Cash, Amount = request.Amount, IsIncome = true, Description = $"Cobro cuenta corriente: {entry.Description}" });
        await context.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }
}
