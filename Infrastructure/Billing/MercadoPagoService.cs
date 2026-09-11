using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SalesSaaS.Application.Billing;

namespace SalesSaaS.Infrastructure.Billing;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";
    public string AccessToken { get; init; } = string.Empty;
    public string? NotificationUrl { get; init; }
    public string? SuccessUrl { get; init; }
    public string? FailureUrl { get; init; }
}

public sealed class MercadoPagoService(HttpClient client, IHostEnvironment environment, IOptions<MercadoPagoOptions> options) : IPaymentGatewayService
{
    private readonly MercadoPagoOptions _options = options.Value;
    private bool IsDevelopmentMode => environment.IsDevelopment();

    public async Task<PaymentCheckoutResult> CreateSubscriptionCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
    {
        var externalReference = $"saas-{request.SaaSInvoiceId:N}";
        if (IsDevelopmentMode)
            return new PaymentCheckoutResult("MercadoPago", externalReference, string.Empty, null, true);

        if (string.IsNullOrWhiteSpace(_options.AccessToken)) throw new InvalidOperationException("Mercado Pago no está configurado. Contactá al administrador.");
        using var message = new HttpRequestMessage(HttpMethod.Post, "checkout/preferences");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        var payload = new Dictionary<string, object?>
        {
            ["items"] = new[] { new { title = request.Description, quantity = 1, currency_id = request.Currency, unit_price = request.Amount } },
            ["external_reference"] = externalReference,
            ["metadata"] = new { saasInvoiceId = request.SaaSInvoiceId, tenantId = request.TenantId }
        };
        if (!string.IsNullOrWhiteSpace(_options.NotificationUrl)) payload["notification_url"] = _options.NotificationUrl;
        if (!string.IsNullOrWhiteSpace(_options.SuccessUrl) || !string.IsNullOrWhiteSpace(_options.FailureUrl))
        {
            payload["back_urls"] = new { success = _options.SuccessUrl, failure = _options.FailureUrl, pending = _options.FailureUrl };
            payload["auto_return"] = "approved";
        }
        message.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Mercado Pago no pudo crear el checkout. Verificá las credenciales configuradas.");
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var url = root.TryGetProperty("init_point", out var initPoint) ? initPoint.GetString() : null;
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("Mercado Pago no devolvió una URL de checkout válida.");
        return new PaymentCheckoutResult("MercadoPago", externalReference, url, root.TryGetProperty("id", out var preference) ? preference.GetString() : null, false);
    }

    public async Task<PaymentWebhookResult> ProcessWebhookAsync(string provider, string payload, string? signature, CancellationToken cancellationToken)
    {
        if (!string.Equals(provider, "MercadoPago", StringComparison.OrdinalIgnoreCase)) return new PaymentWebhookResult(false, null, false, null, "Proveedor de pago no soportado.");
        using var incoming = JsonDocument.Parse(payload);
        var root = incoming.RootElement;
        var paymentId = root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var nestedId) ? nestedId.GetString() : root.TryGetProperty("id", out var id) ? id.GetString() : null;
        if (string.IsNullOrWhiteSpace(paymentId)) return new PaymentWebhookResult(false, null, false, null, "El webhook de Mercado Pago no contiene un pago válido.");
        if (IsDevelopmentMode)
        {
            var simulatedReference = root.TryGetProperty("external_reference", out var external) ? external.GetString() : null;
            return new PaymentWebhookResult(!string.IsNullOrWhiteSpace(simulatedReference), simulatedReference, true, paymentId, null, IsSimulated: true);
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/payments/{paymentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return new PaymentWebhookResult(false, null, false, null, "No se pudo verificar el pago informado por Mercado Pago.");
        using var verified = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var payment = verified.RootElement;
        var reference = payment.TryGetProperty("external_reference", out var externalReference) ? externalReference.GetString() : null;
        var status = payment.TryGetProperty("status", out var paymentStatus) ? paymentStatus.GetString() : null;
        return new PaymentWebhookResult(!string.IsNullOrWhiteSpace(reference), reference, string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase), paymentId, null,
            payment.TryGetProperty("transaction_amount", out var amount) ? amount.GetDecimal() : null,
            payment.TryGetProperty("currency_id", out var currency) ? currency.GetString() : null);
    }
}
