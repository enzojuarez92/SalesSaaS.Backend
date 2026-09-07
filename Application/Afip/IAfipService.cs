using SalesSaaS.Domain;

namespace SalesSaaS.Application.Afip;

public interface IAfipService
{
    Task<long> GetLastAuthorizedVoucherAsync(TenantFiscalProfile profile, AfipVoucherType voucherType, CancellationToken cancellationToken);
    Task<AfipAuthorizationResponse> AuthorizeInvoiceAsync(TenantFiscalProfile profile, AfipAuthorizationRequest request, CancellationToken cancellationToken);
}

public sealed record AfipVatItem(int Id, decimal BaseAmount, decimal Amount);
public sealed record AfipAuthorizationRequest(
    AfipVoucherType VoucherType,
    long VoucherNumber,
    AfipConcept Concept,
    int CustomerDocumentType,
    long CustomerDocumentNumber,
    decimal TotalAmount,
    decimal NetAmount,
    decimal VatAmount,
    decimal ExemptAmount,
    IReadOnlyList<AfipVatItem> VatItems,
    DateOnly InvoiceDate,
    DateOnly? ServiceStartDate,
    DateOnly? ServiceEndDate,
    DateOnly? PaymentDueDate);
public sealed record AfipAuthorizationResponse(bool IsApproved, string? Cae, DateOnly? CaeExpirationDate, string? Errors);
