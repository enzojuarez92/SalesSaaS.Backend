using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Application.Billing;
using SalesSaaS.Application.Common;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Sales;

public sealed record QuoteItemRequest(Guid ProductId, int Quantity);
public sealed record CreateQuoteCommand(Guid TenantId, Guid CustomerId, DateTime ExpiresAtUtc, List<QuoteItemRequest> Items) : IRequest<Guid>, ITenantScopedRequest;
public sealed record CreateInvoiceFromOrderCommand(Guid TenantId, Guid OrderId, string Number, DateTime? DueAtUtc) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetCustomerAccountQuery(Guid TenantId, Guid CustomerId) : IRequest<IReadOnlyList<CustomerAccountEntryDto>>, ITenantScopedRequest;
public sealed record CustomerAccountEntryDto(Guid Id, CustomerAccountEntryType Type, decimal Amount, string Description, DateTime OccurredAtUtc);
public sealed record GetInvoicesQuery(Guid TenantId, string? Status, int PageNumber = 1, int PageSize = 20, DateOnly? DateFrom = null, DateOnly? DateTo = null, string? Customer = null) : IRequest<PagedResult<InvoiceDto>>, ITenantScopedRequest;
public sealed record InvoiceDto(Guid Id, Guid OrderId, string Number, string Status, decimal TotalAmount, DateTime IssuedAtUtc, string CustomerName, string? Cae, DateOnly? CaeExpirationDate, string? AfipResult, string? BarCode, string? AfipErrors, AfipVoucherType? AfipVoucherType, int? AfipSalesPoint);

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
public sealed class CreateInvoiceFromOrderCommandHandler(ApplicationDbContext context, ISubscriptionGatekeeper subscriptionGatekeeper) : IRequestHandler<CreateInvoiceFromOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateInvoiceFromOrderCommand request, CancellationToken cancellationToken)
    {
        await subscriptionGatekeeper.EnsureCanIssueInvoiceAsync(request.TenantId, cancellationToken);
        var order = await context.Orders.Include(item => item.Items).SingleOrDefaultAsync(item => item.Id == request.OrderId && item.TenantId == request.TenantId, cancellationToken) ?? throw new InvalidOperationException("El pedido no existe.");
        if (order.Status == "Cancelled") throw new InvalidOperationException("No se puede facturar un pedido cancelado.");
        if (await context.Invoices.AnyAsync(item => item.OrderId == order.Id && item.Status == "Issued", cancellationToken)) throw new InvalidOperationException("El pedido ya posee una factura emitida.");
        var invoice = new Invoice { Id = Guid.NewGuid(), TenantId = request.TenantId, OrderId = order.Id, CustomerId = order.CustomerId, Number = request.Number.Trim(), TotalAmount = order.TotalAmount, DueAtUtc = request.DueAtUtc };
        context.Invoices.Add(invoice);
        if (order.PaymentMethod == PaymentMethod.Account)
            context.CustomerAccountEntries.Add(new CustomerAccountEntry { Id = Guid.NewGuid(), TenantId = request.TenantId, CustomerId = order.CustomerId, WarehouseId = order.WarehouseId, InvoiceId = invoice.Id, Type = CustomerAccountEntryType.Debit, Amount = order.TotalAmount, Description = $"Factura {invoice.Number}" });
        await context.SaveChangesAsync(cancellationToken); return invoice.Id;
    }
}
public sealed class GetCustomerAccountQueryHandler(ApplicationDbContext context) : IRequestHandler<GetCustomerAccountQuery, IReadOnlyList<CustomerAccountEntryDto>>
{
    public async Task<IReadOnlyList<CustomerAccountEntryDto>> Handle(GetCustomerAccountQuery request, CancellationToken cancellationToken) => await context.CustomerAccountEntries.AsNoTracking().Where(item => item.TenantId == request.TenantId && item.CustomerId == request.CustomerId).OrderByDescending(item => item.OccurredAtUtc).Select(item => new CustomerAccountEntryDto(item.Id, item.Type, item.Amount, item.Description, item.OccurredAtUtc)).ToListAsync(cancellationToken);
}

public sealed class GetInvoicesQueryValidator : AbstractValidator<GetInvoicesQuery>
{
    public GetInvoicesQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.Status).MaximumLength(30).When(query => query.Status is not null);
        RuleFor(query => query.Customer).MaximumLength(150).When(query => query.Customer is not null);
        RuleFor(query => query.DateTo).GreaterThanOrEqualTo(query => query.DateFrom).When(query => query.DateFrom.HasValue && query.DateTo.HasValue);
    }
}

public sealed class GetInvoicesQueryHandler(ApplicationDbContext context) : IRequestHandler<GetInvoicesQuery, PagedResult<InvoiceDto>>
{
    public async Task<PagedResult<InvoiceDto>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Invoice> invoices = context.Invoices.AsNoTracking().Include(invoice => invoice.Customer)
            .Where(invoice => invoice.TenantId == request.TenantId);
        if (!string.IsNullOrWhiteSpace(request.Status))
            invoices = invoices.Where(invoice => invoice.AfipResult == request.Status || invoice.Status == request.Status);
        if (request.DateFrom.HasValue) invoices = invoices.Where(invoice => invoice.IssuedAtUtc >= request.DateFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (request.DateTo.HasValue) invoices = invoices.Where(invoice => invoice.IssuedAtUtc < request.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (!string.IsNullOrWhiteSpace(request.Customer)) invoices = invoices.Where(invoice => invoice.Customer != null && invoice.Customer.Name.Contains(request.Customer));

        var totalCount = await invoices.CountAsync(cancellationToken);
        var items = await invoices.OrderByDescending(invoice => invoice.IssuedAtUtc)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(invoice => new InvoiceDto(invoice.Id, invoice.OrderId, invoice.Number, invoice.Status, invoice.TotalAmount, invoice.IssuedAtUtc,
                invoice.Customer != null ? invoice.Customer.Name : "Consumidor final", invoice.Cae, invoice.CaeExpirationDate,
                invoice.AfipResult, invoice.BarCode, invoice.AfipErrors, invoice.AfipVoucherType, invoice.AfipSalesPoint))
            .ToListAsync(cancellationToken);
        return new PagedResult<InvoiceDto>(items, totalCount, request.PageNumber, request.PageSize);
    }
}
