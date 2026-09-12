using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using SalesSaaS.Application.Afip;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Afip;

public sealed class AfipService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IFiscalProfileSecretProtector secretProtector,
    ILogger<AfipService> logger) : IAfipService
{
    private static readonly XNamespace SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    public async Task<long> GetLastAuthorizedVoucherAsync(TenantFiscalProfile profile, AfipVoucherType voucherType, CancellationToken cancellationToken)
    {
        var ticket = await GetAccessTicketAsync(profile, cancellationToken);
        var body = new XElement("FECompUltimoAutorizado",
            new XAttribute("xmlns", "http://ar.gov.afip.dif.FEV1/"),
            CreateAuthenticationElement(ticket, profile.IssuerTaxId),
            new XElement("PtoVta", profile.SalesPoint),
            new XElement("CbteTipo", (int)voucherType));
        var response = await SendWsfeRequestAsync(profile, body, cancellationToken);
        var value = FindDescendantValue(response, "CbteNro");
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    public async Task<AfipAuthorizationResponse> AuthorizeInvoiceAsync(TenantFiscalProfile profile, AfipAuthorizationRequest request, CancellationToken cancellationToken)
    {
        var ticket = await GetAccessTicketAsync(profile, cancellationToken);
        var detail = new XElement("FECAEDetRequest",
            new XElement("Concepto", (int)request.Concept),
            new XElement("DocTipo", request.CustomerDocumentType),
            new XElement("DocNro", request.CustomerDocumentNumber),
            new XElement("CbteDesde", request.VoucherNumber),
            new XElement("CbteHasta", request.VoucherNumber),
            new XElement("CbteFch", FormatDate(request.InvoiceDate)),
            new XElement("ImpTotal", FormatAmount(request.TotalAmount)),
            new XElement("ImpTotConc", "0"),
            new XElement("ImpNeto", FormatAmount(request.NetAmount)),
            new XElement("ImpOpEx", FormatAmount(request.ExemptAmount)),
            new XElement("ImpTrib", "0"),
            new XElement("ImpIVA", FormatAmount(request.VatAmount)),
            new XElement("FchServDesde", request.ServiceStartDate.HasValue ? FormatDate(request.ServiceStartDate.Value) : null),
            new XElement("FchServHasta", request.ServiceEndDate.HasValue ? FormatDate(request.ServiceEndDate.Value) : null),
            new XElement("FchVtoPago", request.PaymentDueDate.HasValue ? FormatDate(request.PaymentDueDate.Value) : null),
            new XElement("MonId", "PES"),
            new XElement("MonCotiz", "1"));

        if (request.VatItems.Count > 0)
        {
            detail.Add(new XElement("Iva", request.VatItems.Select(item => new XElement("AlicIva",
                new XElement("Id", item.Id),
                new XElement("BaseImp", FormatAmount(item.BaseAmount)),
                new XElement("Importe", FormatAmount(item.Amount))))));
        }
        if (request.AssociatedVoucher is not null)
        {
            detail.Add(new XElement("CbtesAsoc", new XElement("CbteAsoc",
                new XElement("Tipo", (int)request.AssociatedVoucher.VoucherType),
                new XElement("PtoVta", request.AssociatedVoucher.SalesPoint),
                new XElement("Nro", request.AssociatedVoucher.VoucherNumber))));
        }

        var body = new XElement("FECAESolicitar",
            new XAttribute("xmlns", "http://ar.gov.afip.dif.FEV1/"),
            CreateAuthenticationElement(ticket, profile.IssuerTaxId),
            new XElement("FeCAEReq",
                new XElement("FeCabReq",
                    new XElement("CantReg", 1),
                    new XElement("PtoVta", profile.SalesPoint),
                    new XElement("CbteTipo", (int)request.VoucherType)),
                new XElement("FeDetReq", detail)));

        var response = await SendWsfeRequestAsync(profile, body, cancellationToken);
        var result = FindDescendantValue(response, "Resultado");
        var errors = GetAfipErrors(response);
        var cae = FindDescendantValue(response, "CAE");
        var expirationText = FindDescendantValue(response, "CAEFchVto");
        var isApproved = string.Equals(result, "A", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cae);
        return new AfipAuthorizationResponse(isApproved, cae, ParseAfipDate(expirationText), errors);
    }

    private async Task<AfipAccessTicket> GetAccessTicketAsync(TenantFiscalProfile profile, CancellationToken cancellationToken)
    {
        var cacheKey = $"afip-access-ticket:{profile.TenantId:N}:{profile.Environment}";
        if (cache.TryGetValue(cacheKey, out AfipAccessTicket? cached) && cached is not null && cached.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(5)) return cached;

        var tra = BuildTicketRequest();
        var cms = SignTicketRequest(tra, profile);
        var response = await SendWsaaRequestAsync(profile, cms, cancellationToken);
        var ticketXml = XDocument.Parse(FindDescendantValue(response, "loginCmsReturn") ?? throw new InvalidOperationException("AFIP no devolvió un Ticket de Acceso válido."));
        var token = FindDescendantValue(ticketXml, "token") ?? throw new InvalidOperationException("AFIP no devolvió el token de acceso.");
        var sign = FindDescendantValue(ticketXml, "sign") ?? throw new InvalidOperationException("AFIP no devolvió la firma de acceso.");
        var expiration = DateTime.TryParse(FindDescendantValue(ticketXml, "expirationTime"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedExpiration)
            ? parsedExpiration : DateTime.UtcNow.AddMinutes(10);
        var ticket = new AfipAccessTicket(token, sign, expiration);
        cache.Set(cacheKey, ticket, expiration.AddMinutes(-5));
        return ticket;
    }

    private string SignTicketRequest(string tra, TenantFiscalProfile profile)
    {
        try
        {
            using var certificate = LoadCertificate(profile);
            var content = new ContentInfo(Encoding.UTF8.GetBytes(tra));
            var signedCms = new SignedCms(content, detached: false);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate) { IncludeOption = X509IncludeOption.EndCertOnly };
            signedCms.ComputeSignature(signer);
            return Convert.ToBase64String(signedCms.Encode());
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidOperationException)
        {
            logger.LogError(exception, "Unable to load or use the AFIP certificate for tenant {TenantId}", profile.TenantId);
            throw new InvalidOperationException("No se pudo utilizar el certificado fiscal configurado.");
        }
    }

    private X509Certificate2 LoadCertificate(TenantFiscalProfile profile)
    {
        var certificateContent = secretProtector.Unprotect(profile.CertificateContentEncrypted);
        var privateKeyContent = string.IsNullOrWhiteSpace(profile.PrivateKeyContentEncrypted) ? null : secretProtector.Unprotect(profile.PrivateKeyContentEncrypted);
        var passphrase = string.IsNullOrWhiteSpace(profile.CertificatePassphraseEncrypted) ? null : secretProtector.Unprotect(profile.CertificatePassphraseEncrypted);
        if (profile.IsPfxCertificate)
            return X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(certificateContent), passphrase, X509KeyStorageFlags.EphemeralKeySet);
        if (string.IsNullOrWhiteSpace(privateKeyContent)) throw new InvalidOperationException("Falta la clave privada del certificado fiscal.");
        return X509Certificate2.CreateFromPem(certificateContent, privateKeyContent);
    }

    private async Task<XDocument> SendWsaaRequestAsync(TenantFiscalProfile profile, string cms, CancellationToken cancellationToken)
    {
        var body = new XElement("loginCms", new XAttribute("xmlns", "http://wsaa.view.sua.dvadac.desein.afip.gov"), new XElement("in0", cms));
        return await SendSoapRequestAsync(GetWsaaUrl(profile.Environment), body, null, cancellationToken);
    }

    private async Task<XDocument> SendWsfeRequestAsync(TenantFiscalProfile profile, XElement body, CancellationToken cancellationToken) =>
        await SendSoapRequestAsync(GetWsfeUrl(profile.Environment), body, $"http://ar.gov.afip.dif.FEV1/{body.Name.LocalName}", cancellationToken);

    private async Task<XDocument> SendSoapRequestAsync(string endpoint, XElement body, string? soapAction, CancellationToken cancellationToken)
    {
        var envelope = new XDocument(new XElement(SoapEnvelopeNamespace + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", SoapEnvelopeNamespace), new XElement(SoapEnvelopeNamespace + "Body", body)));
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml")
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        if (!string.IsNullOrWhiteSpace(soapAction)) request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");
        var client = httpClientFactory.CreateClient("Afip");
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("AFIP SOAP call to {Endpoint} failed with status {StatusCode}: {Payload}", endpoint, (int)response.StatusCode, payload);
            throw new InvalidOperationException("AFIP no respondió correctamente. Intentá nuevamente más tarde.");
        }
        var document = XDocument.Parse(payload);
        var fault = FindDescendantValue(document, "faultstring");
        if (!string.IsNullOrWhiteSpace(fault)) throw new InvalidOperationException($"AFIP informó un error: {fault}");
        return document;
    }

    private static XElement CreateAuthenticationElement(AfipAccessTicket ticket, string issuerTaxId) => new("Auth", new XElement("Token", ticket.Token), new XElement("Sign", ticket.Sign), new XElement("Cuit", issuerTaxId));

    private static string BuildTicketRequest()
    {
        var now = DateTime.UtcNow;
        return new XDocument(new XDeclaration("1.0", "UTF-8", null), new XElement("loginTicketRequest", new XAttribute("version", "1.0"), new XElement("header", new XElement("uniqueId", now.Ticks), new XElement("generationTime", now.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)), new XElement("expirationTime", now.AddHours(12).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))), new XElement("service", "wsfe"))).ToString(SaveOptions.DisableFormatting);
    }

    private static string GetWsaaUrl(AfipEnvironment environment) => environment == AfipEnvironment.Production ? "https://wsaa.afip.gov.ar/ws/services/LoginCms" : "https://wsaahomo.afip.gov.ar/ws/services/LoginCms";
    private static string GetWsfeUrl(AfipEnvironment environment) => environment == AfipEnvironment.Production ? "https://servicios1.afip.gov.ar/wsfev1/service.asmx" : "https://wswhomo.afip.gov.ar/wsfev1/service.asmx";
    private static string FormatDate(DateOnly value) => value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    private static string FormatAmount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string? FindDescendantValue(XContainer document, string localName) => document.Descendants().FirstOrDefault(element => element.Name.LocalName == localName)?.Value;
    private static DateOnly? ParseAfipDate(string? value) => DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    private static string? GetAfipErrors(XContainer response)
    {
        var messages = response.Descendants().Where(element => element.Name.LocalName is "Err" or "Obs").Select(element => string.Join(" - ", element.Elements().Select(child => child.Value))).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        return messages.Count == 0 ? null : string.Join(" | ", messages);
    }

    private sealed record AfipAccessTicket(string Token, string Sign, DateTime ExpiresAtUtc);
}
