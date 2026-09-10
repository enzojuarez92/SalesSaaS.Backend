namespace SalesSaaS.Application.Billing;

public interface IPaymentGatewayService
{
    Task<PaymentCheckoutResult> CreateSubscriptionCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken);
    Task<PaymentWebhookResult> ProcessWebhookAsync(string provider, string payload, string? signature, CancellationToken cancellationToken);
}

public sealed record PaymentCheckoutRequest(Guid SaaSInvoiceId, Guid TenantId, decimal Amount, string Currency, string Description, string Provider);
public sealed record PaymentCheckoutResult(string Provider, string ExternalReference, string CheckoutUrl, string? ProviderSubscriptionId, bool IsSimulated);
public sealed record PaymentWebhookResult(bool IsValid, string? ExternalReference, bool IsPaid, string? ProviderSubscriptionId, string? Error);
