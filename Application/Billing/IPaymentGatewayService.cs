namespace SalesSaaS.Application.Billing;

public interface IPaymentGatewayService
{
    Task<PaymentCheckoutResult> CreateSubscriptionCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken);
    Task<PaymentWebhookResult> ProcessWebhookAsync(string provider, string payload, PaymentWebhookHeaders headers, CancellationToken cancellationToken);
}

public sealed record PaymentCheckoutRequest(Guid SaaSInvoiceId, Guid TenantId, decimal Amount, string Currency, string Description, string Provider);
public sealed record PaymentCheckoutResult(string Provider, string ExternalReference, string CheckoutUrl, string? ProviderSubscriptionId, bool IsSimulated);
public sealed record PaymentWebhookHeaders(string? Signature, string? RequestId);
public sealed record PaymentWebhookResult(bool IsValid, string? ExternalReference, bool IsPaid, string? ProviderSubscriptionId, string? Error, string? EventType = null, string? ExternalEventId = null, decimal? Amount = null, string? Currency = null, bool IsSimulated = false);
