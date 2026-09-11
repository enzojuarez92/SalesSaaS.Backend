using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Features.Afip;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Sales;

public enum InvoiceDocumentType { Auto, InvoiceA, InvoiceB, InvoiceC, CreditNoteA, CreditNoteB, CreditNoteC, InternalTicket }
public sealed record IssueInvoiceCommand(Guid TenantId, Guid OrderId, InvoiceDocumentType DocumentType = InvoiceDocumentType.Auto, string? Number = null) : IRequest<InvoiceIssueResultDto>, ITenantScopedRequest;
public sealed record InvoiceIssueResultDto(Guid InvoiceId, string Number, string Status, AfipVoucherType? VoucherType, string? Cae, DateOnly? CaeExpirationDate, string? QrUrl, string? Errors);

public sealed class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceCommandValidator() { RuleFor(command => command.TenantId).NotEmpty(); RuleFor(command => command.OrderId).NotEmpty(); RuleFor(command => command.DocumentType).IsInEnum(); RuleFor(command => command.Number).MaximumLength(50).When(command => command.Number is not null); }
}

public sealed class IssueInvoiceCommandHandler(ApplicationDbContext context, ISender sender) : IRequestHandler<IssueInvoiceCommand, InvoiceIssueResultDto>
{
    public async Task<InvoiceIssueResultDto> Handle(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        var order = await context.Orders.SingleOrDefaultAsync(item => item.Id == request.OrderId && item.TenantId == request.TenantId, cancellationToken) ?? throw new InvalidOperationException("La venta no existe.");
        if (order.Status == "Cancelled") throw new InvalidOperationException("No se puede emitir un comprobante para una venta anulada.");
        var customer = await context.Customers.SingleAsync(item => item.Id == order.CustomerId && item.TenantId == request.TenantId, cancellationToken);
        var documentType = ResolveDocumentType(request.DocumentType, customer);
        if (documentType is not InvoiceDocumentType.CreditNoteA and not InvoiceDocumentType.CreditNoteB and not InvoiceDocumentType.CreditNoteC)
        {
            var previous = await context.Invoices.AsNoTracking().Where(i => i.OrderId == order.Id && i.Status != "Cancelled" && i.AfipVoucherType == ToVoucherType(documentType)).OrderBy(i => i.IssuedAtUtc).FirstOrDefaultAsync(cancellationToken);
            if (previous is not null) return new(previous.Id, previous.Number, previous.Status, previous.AfipVoucherType, previous.Cae, previous.CaeExpirationDate, previous.BarCode, previous.AfipErrors);
        }
        if (documentType != InvoiceDocumentType.InternalTicket && documentType is not InvoiceDocumentType.CreditNoteA and not InvoiceDocumentType.CreditNoteB and not InvoiceDocumentType.CreditNoteC && await context.Invoices.AnyAsync(item => item.OrderId == order.Id && item.Status == "Issued" && item.AfipVoucherType != AfipVoucherType.CreditNoteA && item.AfipVoucherType != AfipVoucherType.CreditNoteB && item.AfipVoucherType != AfipVoucherType.CreditNoteC, cancellationToken)) throw new InvalidOperationException("La venta ya posee un comprobante fiscal emitido.");
        var number = string.IsNullOrWhiteSpace(request.Number) ? $"POS-{DateTime.UtcNow:yyyyMMddHHmmssfff}" : request.Number.Trim();
        var voucherType = ToVoucherType(documentType);
        var invoice = new Invoice { Id = Guid.NewGuid(), TenantId = request.TenantId, OrderId = order.Id, CustomerId = customer.Id, Number = number, TotalAmount = order.TotalAmount, Status = voucherType is null ? "Issued" : "Pending", AfipResult = voucherType is null ? "Internal" : "Pending", AfipVoucherType = voucherType };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);
        if (voucherType is null) return new InvoiceIssueResultDto(invoice.Id, invoice.Number, invoice.Status, null, null, null, null, null);
        var net = decimal.Round(invoice.TotalAmount / 1.21m, 2);
        var vat = decimal.Round(invoice.TotalAmount - net, 2);
        try
        {
            var result = await sender.Send(new AuthorizeInvoiceCommand(request.TenantId, invoice.Id, voucherType.Value, AfipConcept.Products, 0, [new AfipVatItemRequest(5, net, vat)], null, null, null), cancellationToken);
            var refreshed = await context.Invoices.AsNoTracking().SingleAsync(item => item.Id == invoice.Id, cancellationToken);
            return new InvoiceIssueResultDto(invoice.Id, invoice.Number, refreshed.Status, voucherType, result.Cae, result.CaeExpirationDate, result.BarCode, result.Errors);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            var refreshed = await context.Invoices.AsNoTracking().SingleAsync(item => item.Id == invoice.Id, cancellationToken);
            return new InvoiceIssueResultDto(invoice.Id, invoice.Number, refreshed.Status, voucherType, refreshed.Cae, refreshed.CaeExpirationDate, refreshed.BarCode, refreshed.AfipErrors ?? "No se pudo completar la autorización. Revisá el estado del comprobante antes de reintentar.");
        }
    }
    private static InvoiceDocumentType ResolveDocumentType(InvoiceDocumentType requested, Customer customer) => requested != InvoiceDocumentType.Auto ? requested : customer.TaxCondition.Contains("Responsable", StringComparison.OrdinalIgnoreCase) ? InvoiceDocumentType.InvoiceA : InvoiceDocumentType.InvoiceB;
    private static AfipVoucherType? ToVoucherType(InvoiceDocumentType type) => type switch { InvoiceDocumentType.InvoiceA => AfipVoucherType.InvoiceA, InvoiceDocumentType.InvoiceB => AfipVoucherType.InvoiceB, InvoiceDocumentType.InvoiceC => AfipVoucherType.InvoiceC, InvoiceDocumentType.CreditNoteA => AfipVoucherType.CreditNoteA, InvoiceDocumentType.CreditNoteB => AfipVoucherType.CreditNoteB, InvoiceDocumentType.CreditNoteC => AfipVoucherType.CreditNoteC, _ => null };
}
