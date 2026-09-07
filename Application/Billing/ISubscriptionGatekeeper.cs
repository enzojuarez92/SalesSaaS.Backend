namespace SalesSaaS.Application.Billing;

public interface ISubscriptionGatekeeper
{
    Task EnsureActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken);
    Task EnsureCanAddUserAsync(Guid tenantId, CancellationToken cancellationToken);
    Task EnsureCanAddWarehouseAsync(Guid tenantId, CancellationToken cancellationToken);
    Task EnsureCanIssueInvoiceAsync(Guid tenantId, CancellationToken cancellationToken);
    Task EnsureAfipIsAvailableAsync(Guid tenantId, CancellationToken cancellationToken);
}
