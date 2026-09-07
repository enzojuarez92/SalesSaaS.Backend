using System.Text.Json;
using SalesSaaS.Application.Billing;

namespace SalesSaaS.Infrastructure.Billing;

public sealed class DevelopmentPaymentGatewayService : IPaymentGatewayService
{
    public Task<PaymentCheckoutResult> CreateSubscriptionCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
    {
        var externalReference = $"saas-{request.SaaSInvoiceId:N}";
        var checkoutUrl = $"https://checkout.example.invalid/{request.Provider.ToLowerInvariant()}/{externalReference}";
        return Task.FromResult(new PaymentCheckoutResult(request.Provider, externalReference, checkoutUrl, null));
    }

    public Task<PaymentWebhookResult> ProcessWebhookAsync(string provider, string payload, string? signature, CancellationToken cancellationToken)
    {
        try
        {
            var message = JsonSerializer.Deserialize<DevelopmentWebhookMessage>(payload);
            return Task.FromResult(message is null || string.IsNullOrWhiteSpace(message.ExternalReference)
                ? new PaymentWebhookResult(false, null, false, null, "El webhook no contiene una referencia externa válida.")
                : new PaymentWebhookResult(true, message.ExternalReference, string.Equals(message.Status, "paid", StringComparison.OrdinalIgnoreCase), message.ProviderSubscriptionId, null));
        }
        catch (JsonException)
        {
            return Task.FromResult(new PaymentWebhookResult(false, null, false, null, "El webhook no tiene un formato JSON válido."));
        }
    }

    private sealed record DevelopmentWebhookMessage(string ExternalReference, string Status, string? ProviderSubscriptionId);
}
