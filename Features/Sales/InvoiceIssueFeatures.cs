using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Afip;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Features.Afip;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Sales;

public enum InvoiceDocumentType { Auto, InvoiceA, InvoiceB, InvoiceC, CreditNoteA, CreditNoteB, CreditNoteC, InternalTicket }
public sealed record IssueInvoiceCommand(Guid TenantId, Guid OrderId, InvoiceDocumentType DocumentType = InvoiceDocumentType.Auto, string? Number = null, Guid? AssociatedInvoiceId = null) : IRequest<InvoiceIssueResultDto>, ITenantScopedRequest;
public sealed record InvoiceIssueResultDto(Guid InvoiceId, string Number, string Status, AfipVoucherType? VoucherType, string? Cae, DateOnly? CaeExpirationDate, string? QrUrl, string? Errors);

public sealed class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
{
    public IssueInvoiceCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.DocumentType).IsInEnum();
        RuleFor(command => command.Number).MaximumLength(50).When(command => command.Number is not null);
        RuleFor(command => command.AssociatedInvoiceId).NotEmpty().When(command => command.DocumentType is InvoiceDocumentType.CreditNoteA or InvoiceDocumentType.CreditNoteB or InvoiceDocumentType.CreditNoteC)
            .WithMessage("La nota de crédito debe indicar el comprobante asociado.");
    }
}

public sealed class IssueInvoiceCommandHandler(ApplicationDbContext context, ISender sender) : IRequestHandler<IssueInvoiceCommand, InvoiceIssueResultDto>
{
    public async Task<InvoiceIssueResultDto> Handle(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        var order = await context.Orders.Include(item => item.Items).ThenInclude(item => item.Product).SingleOrDefaultAsync(item => item.Id == request.OrderId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("La venta no existe.");
        if (order.Status == "Cancelled") throw new InvalidOperationException("No se puede emitir un comprobante para una venta anulada.");
        var customer = await context.Customers.SingleAsync(item => item.Id == order.CustomerId && item.TenantId == request.TenantId, cancellationToken);
        var tenant = await context.Tenants.AsNoTracking().SingleAsync(item => item.Id == request.TenantId, cancellationToken);
        var associated = await ResolveAssociatedInvoice(request, cancellationToken);
        var voucherType = ResolveVoucherType(request.DocumentType, tenant.TaxCondition, customer.TaxCondition, associated);

        if (voucherType is not null && request.DocumentType is not (InvoiceDocumentType.CreditNoteA or InvoiceDocumentType.CreditNoteB or InvoiceDocumentType.CreditNoteC))
        {
            var previous = await context.Invoices.AsNoTracking().Where(i => i.OrderId == order.Id && i.Status != "Cancelled" && i.AfipVoucherType == voucherType).OrderBy(i => i.IssuedAtUtc).FirstOrDefaultAsync(cancellationToken);
            if (previous is not null)
            {
                if (!string.IsNullOrWhiteSpace(previous.Cae)) return new(previous.Id, previous.Number, previous.Status, previous.AfipVoucherType, previous.Cae, previous.CaeExpirationDate, previous.BarCode, previous.AfipErrors);
                var retryBreakdown = BuildTaxBreakdown(order, voucherType.Value);
                var retry = await sender.Send(new AuthorizeInvoiceCommand(request.TenantId, previous.Id, voucherType.Value, AfipConcept.Products, retryBreakdown.ExemptAmount, retryBreakdown.VatItems, null, null, null), cancellationToken);
                var refreshed = await context.Invoices.AsNoTracking().SingleAsync(item => item.Id == previous.Id, cancellationToken);
                return new(refreshed.Id, refreshed.Number, refreshed.Status, refreshed.AfipVoucherType, retry.Cae, retry.CaeExpirationDate, retry.BarCode, retry.Errors);
            }
            if (await context.Invoices.AnyAsync(item => item.OrderId == order.Id && item.Status == "Issued" && item.AfipVoucherType != AfipVoucherType.CreditNoteA && item.AfipVoucherType != AfipVoucherType.CreditNoteB && item.AfipVoucherType != AfipVoucherType.CreditNoteC, cancellationToken))
                throw new InvalidOperationException("La venta ya posee un comprobante fiscal emitido.");
        }

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(), TenantId = request.TenantId, OrderId = order.Id, CustomerId = customer.Id, AssociatedInvoiceId = associated?.Id,
            Number = string.IsNullOrWhiteSpace(request.Number) ? $"POS-{DateTime.UtcNow:yyyyMMddHHmmssfff}" : request.Number.Trim(),
            TotalAmount = order.TotalAmount, Status = voucherType is null ? "Issued" : "Pending", AfipResult = voucherType is null ? "Internal" : "Pending", AfipVoucherType = voucherType
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(cancellationToken);
        if (voucherType is null) return new(invoice.Id, invoice.Number, invoice.Status, null, null, null, null, null);

        var taxBreakdown = BuildTaxBreakdown(order, voucherType.Value);
        try
        {
            var result = await sender.Send(new AuthorizeInvoiceCommand(request.TenantId, invoice.Id, voucherType.Value, AfipConcept.Products, taxBreakdown.ExemptAmount, taxBreakdown.VatItems, null, null, null, BuildAssociation(associated)), cancellationToken);
            var refreshed = await context.Invoices.AsNoTracking().SingleAsync(item => item.Id == invoice.Id, cancellationToken);
            return new(invoice.Id, invoice.Number, refreshed.Status, voucherType, result.Cae, result.CaeExpirationDate, result.BarCode, result.Errors);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            var refreshed = await context.Invoices.AsNoTracking().SingleAsync(item => item.Id == invoice.Id, cancellationToken);
            return new(invoice.Id, invoice.Number, refreshed.Status, voucherType, refreshed.Cae, refreshed.CaeExpirationDate, refreshed.BarCode, refreshed.AfipErrors ?? "No se pudo completar la autorización. Revisá el estado del comprobante antes de reintentar.");
        }
    }

    private async Task<Invoice?> ResolveAssociatedInvoice(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        var isCredit = request.DocumentType is InvoiceDocumentType.CreditNoteA or InvoiceDocumentType.CreditNoteB or InvoiceDocumentType.CreditNoteC;
        if (!isCredit) return null;
        var associated = await context.Invoices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == request.AssociatedInvoiceId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("El comprobante asociado no existe en este negocio.");
        if (string.IsNullOrWhiteSpace(associated.Cae) || associated.AfipSalesPoint is null)
            throw new InvalidOperationException("Sólo se pueden asociar comprobantes fiscales autorizados con CAE.");
        return associated;
    }

    private static AfipVoucherType? ResolveVoucherType(InvoiceDocumentType requested, string? issuerTaxCondition, string? customerTaxCondition, Invoice? associated)
    {
        if (requested == InvoiceDocumentType.InternalTicket) return null;
        if (requested is InvoiceDocumentType.CreditNoteA or InvoiceDocumentType.CreditNoteB or InvoiceDocumentType.CreditNoteC)
        {
            var expected = FiscalRules.ExpectedCreditNote(associated!.AfipVoucherType!.Value);
            if (ToVoucherType(requested) != expected) throw new InvalidOperationException("La nota de crédito debe tener la misma letra que el comprobante asociado.");
            return expected;
        }
        var expectedInvoice = FiscalRules.ExpectedInvoice(issuerTaxCondition, customerTaxCondition);
        if (requested == InvoiceDocumentType.Auto) return expectedInvoice;
        if (ToVoucherType(requested) != expectedInvoice) throw new InvalidOperationException($"Por la condición fiscal del emisor y receptor corresponde {VoucherLabel(expectedInvoice)}.");
        return expectedInvoice;
    }

    private static AfipVoucherType? ToVoucherType(InvoiceDocumentType type) => type switch { InvoiceDocumentType.InvoiceA => AfipVoucherType.InvoiceA, InvoiceDocumentType.InvoiceB => AfipVoucherType.InvoiceB, InvoiceDocumentType.InvoiceC => AfipVoucherType.InvoiceC, InvoiceDocumentType.CreditNoteA => AfipVoucherType.CreditNoteA, InvoiceDocumentType.CreditNoteB => AfipVoucherType.CreditNoteB, InvoiceDocumentType.CreditNoteC => AfipVoucherType.CreditNoteC, _ => null };
    private static string VoucherLabel(AfipVoucherType type) => type switch { AfipVoucherType.InvoiceA => "Factura A", AfipVoucherType.InvoiceB => "Factura B", AfipVoucherType.InvoiceC => "Factura C", _ => "el comprobante correcto" };

    private static (decimal ExemptAmount, List<AfipVatItemRequest> VatItems) BuildTaxBreakdown(Order order, AfipVoucherType voucherType)
    {
        if (voucherType is AfipVoucherType.InvoiceC or AfipVoucherType.CreditNoteC) return (0m, []);
        var itemsTotal = order.Items.Sum(item => item.SubTotal);
        if (itemsTotal <= 0) throw new InvalidOperationException("La venta no posee importes válidos para facturar.");
        var groups = new Dictionary<int, (decimal Net, decimal Vat)>();
        decimal allocated = 0m;
        for (var index = 0; index < order.Items.Count; index++)
        {
            var item = order.Items.ElementAt(index);
            var discount = index == order.Items.Count - 1 ? order.DiscountAmount - allocated : decimal.Round(order.DiscountAmount * item.SubTotal / itemsTotal, 2);
            allocated += discount;
            var gross = decimal.Round(item.SubTotal - discount, 2);
            var rate = item.Product?.VatRate ?? 21m;
            var vatId = FiscalRules.VatId(rate);
            var net = rate == 0m ? gross : decimal.Round(gross / (1m + rate / 100m), 2);
            var vat = decimal.Round(gross - net, 2);
            groups.TryGetValue(vatId, out var total);
            groups[vatId] = (total.Net + net, total.Vat + vat);
        }
        return (0m, groups.OrderBy(item => item.Key).Select(item => new AfipVatItemRequest(item.Key, decimal.Round(item.Value.Net, 2), decimal.Round(item.Value.Vat, 2))).ToList());
    }

    private static AfipAssociatedVoucher? BuildAssociation(Invoice? invoice)
    {
        if (invoice is null) return null;
        var parts = invoice.Number.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var point) || !long.TryParse(parts[1], out var number)) throw new InvalidOperationException("El comprobante asociado no tiene una numeración fiscal válida.");
        return new AfipAssociatedVoucher(invoice.AfipVoucherType!.Value, point, number);
    }
}
