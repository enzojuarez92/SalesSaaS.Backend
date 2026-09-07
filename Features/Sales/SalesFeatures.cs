using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Sales;

public sealed record QuoteItemRequest(Guid ProductId, int Quantity);
public sealed record CreateQuoteCommand(Guid TenantId, Guid CustomerId, DateTime ExpiresAtUtc, List<QuoteItemRequest> Items) : IRequest<Guid>, ITenantScopedRequest;
public sealed record CreateInvoiceFromOrderCommand(Guid TenantId, Guid OrderId, string Number, DateTime? DueAtUtc) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetCustomerAccountQuery(Guid TenantId, Guid CustomerId) : IRequest<IReadOnlyList<CustomerAccountEntryDto>>, ITenantScopedRequest;
public sealed record CustomerAccountEntryDto(Guid Id, CustomerAccountEntryType Type, decimal Amount, string Description, DateTime OccurredAtUtc);

public sealed class CreateQuoteCommandValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio."); RuleFor(command => command.CustomerId).NotEmpty().WithMessage("El cliente es obligatorio."); RuleFor(command => command.ExpiresAtUtc).GreaterThan(DateTime.UtcNow).WithMessage("La fecha de vencimiento debe ser futura.");
        RuleFor(command => command.Items).NotEmpty().WithMessage("El presupuesto debe tener al menos un producto."); RuleForEach(command => command.Items).ChildRules(item => { item.RuleFor(x => x.ProductId).NotEmpty(); item.RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero."); });
    }
}
public sealed class CreateQuoteCommandHandler(ApplicationDbContext context) : IRequestHandler<CreateQuoteCommand, Guid>
{
    public async Task<Guid> Handle(CreateQuoteCommand request, CancellationToken cancellationToken)
    {
        if (!await context.Customers.AnyAsync(item => item.Id == request.CustomerId && item.TenantId == request.TenantId && item.IsActive, cancellationToken)) throw new InvalidOperationException("El cliente no existe o no está activo.");
        var products = await context.Products.Where(item => item.TenantId == request.TenantId && request.Items.Select(line => line.ProductId).Contains(item.Id) && item.IsActive).ToListAsync(cancellationToken);
        if (products.Count != request.Items.Select(item => item.ProductId).Distinct().Count()) throw new InvalidOperationException("Uno o más productos no existen o no están activos.");
        var quote = new Quote { Id = Guid.NewGuid(), TenantId = request.TenantId, CustomerId = request.CustomerId, ExpiresAtUtc = request.ExpiresAtUtc };
        foreach (var line in request.Items) { var product = products.Single(item => item.Id == line.ProductId); var total = product.Price * line.Quantity; quote.TotalAmount += total; quote.Items.Add(new QuoteItem { Id = Guid.NewGuid(), TenantId = request.TenantId, QuoteId = quote.Id, ProductId = product.Id, Quantity = line.Quantity, UnitPrice = product.Price, TotalAmount = total }); }
        context.Quotes.Add(quote); await context.SaveChangesAsync(cancellationToken); return quote.Id;
    }
}
public sealed class CreateInvoiceFromOrderCommandHandler(ApplicationDbContext context) : IRequestHandler<CreateInvoiceFromOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateInvoiceFromOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await context.Orders.Include(item => item.Items).SingleOrDefaultAsync(item => item.Id == request.OrderId && item.TenantId == request.TenantId, cancellationToken) ?? throw new InvalidOperationException("El pedido no existe.");
        if (order.Status == "Cancelled") throw new InvalidOperationException("No se puede facturar un pedido cancelado.");
        if (await context.Invoices.AnyAsync(item => item.OrderId == order.Id && item.Status == "Issued", cancellationToken)) throw new InvalidOperationException("El pedido ya posee una factura emitida.");
        var invoice = new Invoice { Id = Guid.NewGuid(), TenantId = request.TenantId, OrderId = order.Id, CustomerId = order.CustomerId, Number = request.Number.Trim(), TotalAmount = order.TotalAmount, DueAtUtc = request.DueAtUtc };
        context.Invoices.Add(invoice);
        context.CustomerAccountEntries.Add(new CustomerAccountEntry { Id = Guid.NewGuid(), TenantId = request.TenantId, CustomerId = order.CustomerId, InvoiceId = invoice.Id, Type = CustomerAccountEntryType.Debit, Amount = order.TotalAmount, Description = $"Factura {invoice.Number}" });
        await context.SaveChangesAsync(cancellationToken); return invoice.Id;
    }
}
public sealed class GetCustomerAccountQueryHandler(ApplicationDbContext context) : IRequestHandler<GetCustomerAccountQuery, IReadOnlyList<CustomerAccountEntryDto>>
{
    public async Task<IReadOnlyList<CustomerAccountEntryDto>> Handle(GetCustomerAccountQuery request, CancellationToken cancellationToken) => await context.CustomerAccountEntries.AsNoTracking().Where(item => item.TenantId == request.TenantId && item.CustomerId == request.CustomerId).OrderByDescending(item => item.OccurredAtUtc).Select(item => new CustomerAccountEntryDto(item.Id, item.Type, item.Amount, item.Description, item.OccurredAtUtc)).ToListAsync(cancellationToken);
}
