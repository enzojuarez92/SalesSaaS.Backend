using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Afip;
using SalesSaaS.Application.Billing;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using SalesSaaS.Infrastructure.Afip;
using SalesSaaS.Features.Notifications;
using SalesSaaS.Application.Validation;

namespace SalesSaaS.Features.Afip;

public sealed record ConfigureTenantFiscalProfileCommand(Guid TenantId, string IssuerTaxId, string CertificateContent, string? PrivateKeyContent, string? CertificatePassphrase, string CertificateAlias, bool IsPfxCertificate, AfipEnvironment Environment, int SalesPoint, AfipConcept DefaultConcept) : IRequest<Guid>, ITenantScopedRequest;
public sealed record GetTenantFiscalProfileQuery(Guid TenantId) : IRequest<TenantFiscalProfileDto?>, ITenantScopedRequest;
public sealed record TenantFiscalProfileDto(Guid Id, string IssuerTaxId, string CertificateAlias, bool IsPfxCertificate, AfipEnvironment Environment, int SalesPoint, AfipConcept DefaultConcept, bool IsActive, DateTime UpdatedAtUtc);
public sealed record AfipVatItemRequest(int Id, decimal BaseAmount, decimal Amount);
public sealed record AuthorizeInvoiceCommand(Guid TenantId, Guid InvoiceId, AfipVoucherType VoucherType, AfipConcept Concept, decimal ExemptAmount, List<AfipVatItemRequest> VatItems, DateOnly? ServiceStartDate, DateOnly? ServiceEndDate, DateOnly? PaymentDueDate) : IRequest<AfipInvoiceAuthorizationDto>, ITenantScopedRequest;
public sealed record AfipInvoiceAuthorizationDto(Guid InvoiceId, bool IsApproved, string? Cae, DateOnly? CaeExpirationDate, string? BarCode, string? Errors);

public sealed class ConfigureTenantFiscalProfileCommandValidator : AbstractValidator<ConfigureTenantFiscalProfileCommand>
{
    public ConfigureTenantFiscalProfileCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.IssuerTaxId).Must(ArgentineTaxId.IsValid).WithMessage("El CUIT emisor debe contener exactamente 11 dígitos numéricos y ser válido.");
        RuleFor(command => command.CertificateContent).NotEmpty().WithMessage("El certificado fiscal es obligatorio.");
        RuleFor(command => command.CertificateContent).Must(BeBase64).When(command => command.IsPfxCertificate).WithMessage("El certificado PFX debe enviarse codificado en Base64.");
        RuleFor(command => command.PrivateKeyContent).NotEmpty().When(command => !command.IsPfxCertificate).WithMessage("La clave privada es obligatoria para certificados PEM.");
        RuleFor(command => command.CertificateAlias).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100).WithMessage("El alias del certificado es obligatorio y no puede superar los 100 caracteres.");
        RuleFor(command => command.SalesPoint).GreaterThan(0).WithMessage("El punto de venta debe ser mayor a cero.");
    }

    private static bool BeBase64(string value)
    {
        try { Convert.FromBase64String(value); return true; }
        catch (FormatException) { return false; }
    }
}

public sealed class AuthorizeInvoiceCommandValidator : AbstractValidator<AuthorizeInvoiceCommand>
{
    private static readonly int[] ValidVatIds = [3, 4, 5, 6, 8, 9];

    public AuthorizeInvoiceCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.InvoiceId).NotEmpty().WithMessage("La factura es obligatoria.");
        RuleFor(command => command.ExemptAmount).GreaterThanOrEqualTo(0).WithMessage("El importe exento no puede ser negativo.");
        RuleForEach(command => command.VatItems).ChildRules(item =>
        {
            item.RuleFor(vat => vat.Id).Must(ValidVatIds.Contains).WithMessage("La alícuota de IVA no es válida para AFIP.");
            item.RuleFor(vat => vat.BaseAmount).GreaterThanOrEqualTo(0).WithMessage("La base imponible no puede ser negativa.");
            item.RuleFor(vat => vat.Amount).GreaterThanOrEqualTo(0).WithMessage("El importe de IVA no puede ser negativo.");
        });
        When(command => command.Concept is AfipConcept.Services or AfipConcept.ProductsAndServices, () =>
        {
            RuleFor(command => command.ServiceStartDate).NotNull().WithMessage("La fecha de inicio del servicio es obligatoria.");
            RuleFor(command => command.ServiceEndDate).NotNull().WithMessage("La fecha de fin del servicio es obligatoria.");
            RuleFor(command => command.PaymentDueDate).NotNull().WithMessage("La fecha de vencimiento de pago es obligatoria.");
        });
    }
}

public sealed class ConfigureTenantFiscalProfileCommandHandler(ApplicationDbContext context, IFiscalProfileSecretProtector secretProtector) : IRequestHandler<ConfigureTenantFiscalProfileCommand, Guid>
{
    public async Task<Guid> Handle(ConfigureTenantFiscalProfileCommand request, CancellationToken cancellationToken)
    {
        if (!await context.Tenants.AnyAsync(item => item.Id == request.TenantId && item.IsActive, cancellationToken)) throw new InvalidOperationException("El negocio no existe o no está activo.");
        var profile = await context.TenantFiscalProfiles.SingleOrDefaultAsync(item => item.TenantId == request.TenantId, cancellationToken);
        if (profile is null)
        {
            profile = new TenantFiscalProfile { Id = Guid.NewGuid(), TenantId = request.TenantId };
            context.TenantFiscalProfiles.Add(profile);
        }
        profile.IssuerTaxId = NormalizeTaxId(request.IssuerTaxId);
        profile.CertificateContentEncrypted = secretProtector.Protect(request.CertificateContent.Trim());
        profile.PrivateKeyContentEncrypted = string.IsNullOrWhiteSpace(request.PrivateKeyContent) ? null : secretProtector.Protect(request.PrivateKeyContent.Trim());
        profile.CertificatePassphraseEncrypted = string.IsNullOrWhiteSpace(request.CertificatePassphrase) ? null : secretProtector.Protect(request.CertificatePassphrase);
        profile.CertificateAlias = request.CertificateAlias.Trim();
        profile.IsPfxCertificate = request.IsPfxCertificate;
        profile.Environment = request.Environment;
        profile.SalesPoint = request.SalesPoint;
        profile.DefaultConcept = request.DefaultConcept;
        profile.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }

    private static string NormalizeTaxId(string value) => value.Trim();
}

public sealed class GetTenantFiscalProfileQueryHandler(ApplicationDbContext context) : IRequestHandler<GetTenantFiscalProfileQuery, TenantFiscalProfileDto?>
{
    public async Task<TenantFiscalProfileDto?> Handle(GetTenantFiscalProfileQuery request, CancellationToken cancellationToken) =>
        await context.TenantFiscalProfiles.AsNoTracking().Where(item => item.TenantId == request.TenantId)
            .Select(item => new TenantFiscalProfileDto(item.Id, item.IssuerTaxId, item.CertificateAlias, item.IsPfxCertificate, item.Environment, item.SalesPoint, item.DefaultConcept, item.IsActive, item.UpdatedAtUtc)).SingleOrDefaultAsync(cancellationToken);
}

public sealed class AuthorizeInvoiceCommandHandler(ApplicationDbContext context, IAfipService afipService, ISubscriptionGatekeeper subscriptionGatekeeper, IPublisher publisher, ILogger<AuthorizeInvoiceCommandHandler> logger) : IRequestHandler<AuthorizeInvoiceCommand, AfipInvoiceAuthorizationDto>
{
    public async Task<AfipInvoiceAuthorizationDto> Handle(AuthorizeInvoiceCommand request, CancellationToken cancellationToken)
    {
        await subscriptionGatekeeper.EnsureAfipIsAvailableAsync(request.TenantId, cancellationToken);
        var invoice = await context.Invoices.SingleOrDefaultAsync(item => item.Id == request.InvoiceId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("La factura no existe.");
        if (invoice.Status is not ("Issued" or "Pending")) throw new InvalidOperationException("Sólo se pueden autorizar comprobantes pendientes o emitidos.");
        if (!string.IsNullOrWhiteSpace(invoice.Cae)) throw new InvalidOperationException("La factura ya posee un CAE autorizado.");
        var profile = await context.TenantFiscalProfiles.SingleOrDefaultAsync(item => item.TenantId == request.TenantId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No existe una configuración fiscal activa para este negocio.");
        var customer = await context.Customers.SingleOrDefaultAsync(item => item.Id == invoice.CustomerId && item.TenantId == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("El cliente de la factura no existe.");
        var vatItems = request.VatItems.Select(item => new AfipVatItem(item.Id, item.BaseAmount, item.Amount)).ToList();
        var netAmount = vatItems.Sum(item => item.BaseAmount);
        var vatAmount = vatItems.Sum(item => item.Amount);
        if (decimal.Round(netAmount + vatAmount + request.ExemptAmount, 2) != decimal.Round(invoice.TotalAmount, 2)) throw new InvalidOperationException("Los importes de IVA y exento no coinciden con el total de la factura.");
        var (documentType, documentNumber) = GetCustomerDocument(customer);

        try
        {
            var lastVoucherNumber = await afipService.GetLastAuthorizedVoucherAsync(profile, request.VoucherType, cancellationToken);
            var authorization = await afipService.AuthorizeInvoiceAsync(profile, new AfipAuthorizationRequest(request.VoucherType, lastVoucherNumber + 1, request.Concept, documentType, documentNumber, invoice.TotalAmount, netAmount, vatAmount, request.ExemptAmount, vatItems, DateOnly.FromDateTime(invoice.IssuedAtUtc), request.ServiceStartDate, request.ServiceEndDate, request.PaymentDueDate), cancellationToken);
            invoice.AfipVoucherType = request.VoucherType;
            invoice.AfipSalesPoint = profile.SalesPoint;
            invoice.AfipResult = authorization.IsApproved ? "Approved" : "Rejected";
            invoice.Status = authorization.IsApproved ? "Issued" : "Rejected";
            invoice.Cae = authorization.Cae;
            invoice.CaeExpirationDate = authorization.CaeExpirationDate;
            invoice.AfipErrors = authorization.Errors;
            invoice.BarCode = authorization.IsApproved && authorization.Cae is not null ? BuildAfipQrUrl(profile, request.VoucherType, lastVoucherNumber + 1, invoice, documentType, documentNumber, authorization.Cae) : null;
            await context.SaveChangesAsync(cancellationToken);
            if (authorization.IsApproved && authorization.Cae is not null) await publisher.Publish(new InvoiceAuthorizedEvent(request.TenantId, invoice.Id, invoice.Number, authorization.Cae, customer.Email, customer.Name, invoice.TotalAmount), cancellationToken);
            return new AfipInvoiceAuthorizationDto(invoice.Id, authorization.IsApproved, invoice.Cae, invoice.CaeExpirationDate, invoice.BarCode, invoice.AfipErrors);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "AFIP authorization failed for invoice {InvoiceId} and tenant {TenantId}", invoice.Id, request.TenantId);
            invoice.AfipResult = "Rejected";
            invoice.Status = "Rejected";
            invoice.AfipErrors = exception.Message.Length > 4000 ? exception.Message[..4000] : exception.Message;
            await context.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    private static (int DocumentType, long DocumentNumber) GetCustomerDocument(Customer customer)
    {
        var documentType = customer.DocumentType.Trim().ToUpperInvariant() switch { "CUIT" => 80, "CUIL" => 86, "DNI" => 96, _ => 99 };
        var digits = new string(customer.DocumentNumber.Where(char.IsDigit).ToArray());
        if (documentType != 99 && !long.TryParse(digits, out var documentNumber)) throw new InvalidOperationException("El documento del cliente no es válido para AFIP.");
        return documentType == 99 ? (99, 0) : (documentType, long.Parse(digits, CultureInfo.InvariantCulture));
    }

    private static string BuildAfipQrUrl(TenantFiscalProfile profile, AfipVoucherType voucherType, long voucherNumber, Invoice invoice, int documentType, long documentNumber, string cae)
    {
        var payload = new { ver = 1, fecha = invoice.IssuedAtUtc.ToString("yyyy-MM-dd"), cuit = long.Parse(profile.IssuerTaxId, CultureInfo.InvariantCulture), ptoVta = profile.SalesPoint, tipoCmp = (int)voucherType, nroCmp = voucherNumber, importe = invoice.TotalAmount, moneda = "PES", ctz = 1, tipoDocRec = documentType, nroDocRec = documentNumber, tipoCodAut = "E", codAut = long.Parse(cae, CultureInfo.InvariantCulture) };
        return $"https://www.afip.gob.ar/fe/qr/?p={Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))))}";
    }
}
