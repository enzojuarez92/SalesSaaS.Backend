using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Billing;
using SalesSaaS.Application.Exceptions;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Infrastructure.Billing;

public sealed class SubscriptionGatekeeper(ApplicationDbContext context) : ISubscriptionGatekeeper
{
    public async Task EnsureActiveSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        if (subscription.ExpiresAtUtc <= DateTime.UtcNow || subscription.Status is SalesSaaS.Domain.SubscriptionStatus.PastDue or SalesSaaS.Domain.SubscriptionStatus.Canceled)
        {
            var message = subscription.Status == SalesSaaS.Domain.SubscriptionStatus.Trialing
                ? "Tu periodo de prueba de 7 días ha finalizado. Seleccioná un plan para continuar utilizando el sistema."
                : "Tu suscripción no está activa. Seleccioná un plan para continuar utilizando el sistema.";
            throw new SubscriptionAccessException(message);
        }
    }

    public async Task EnsureCanAddUserAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        await EnsureActiveSubscriptionAsync(tenantId, cancellationToken);
        var users = await context.TenantMemberships.CountAsync(membership => membership.TenantId == tenantId && membership.IsActive, cancellationToken);
        if (users >= subscription.SubscriptionPlan.MaxUsers) throw new InvalidOperationException("El plan actual alcanzó el límite de usuarios. Actualizá tu suscripción para agregar más usuarios.");
    }

    public async Task EnsureCanIssueInvoiceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        await EnsureActiveSubscriptionAsync(tenantId, cancellationToken);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var invoices = await context.Invoices.CountAsync(invoice => invoice.TenantId == tenantId && invoice.IssuedAtUtc >= monthStart && invoice.Status == "Issued", cancellationToken);
        if (invoices >= subscription.SubscriptionPlan.MaxInvoicesPerMonth) throw new InvalidOperationException("El plan actual alcanzó el límite mensual de facturas. Actualizá tu suscripción para continuar facturando.");
    }

    public async Task EnsureCanAddWarehouseAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        await EnsureActiveSubscriptionAsync(tenantId, cancellationToken);
        var warehouses = await context.Warehouses.CountAsync(warehouse => warehouse.TenantId == tenantId && warehouse.IsActive, cancellationToken);
        if (warehouses >= subscription.SubscriptionPlan.MaxWarehouses) throw new InvalidOperationException("El plan actual alcanzó el límite de depósitos. Actualizá tu suscripción para agregar más depósitos.");
    }

    public async Task EnsureAfipIsAvailableAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var subscription = await GetCurrentSubscriptionAsync(tenantId, cancellationToken);
        await EnsureActiveSubscriptionAsync(tenantId, cancellationToken);
        if (!subscription.SubscriptionPlan.SupportsAfip) throw new InvalidOperationException("El plan actual no incluye facturación electrónica ARCA.");
    }

    private async Task<TenantSubscriptionWithPlan> GetCurrentSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await context.TenantSubscriptions.AsNoTracking().Where(subscription => subscription.TenantId == tenantId)
            .OrderByDescending(subscription => (subscription.Status == SalesSaaS.Domain.SubscriptionStatus.Active || subscription.Status == SalesSaaS.Domain.SubscriptionStatus.Trialing) && subscription.ExpiresAtUtc > DateTime.UtcNow).ThenByDescending(subscription => subscription.StartsAtUtc)
            .Select(subscription => new TenantSubscriptionWithPlan(subscription.Status, subscription.ExpiresAtUtc, subscription.SubscriptionPlan!.MaxUsers, subscription.SubscriptionPlan.MaxWarehouses, subscription.SubscriptionPlan.MaxInvoicesPerMonth, subscription.SubscriptionPlan.SupportsAfip))
            .FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("El negocio no tiene una suscripción configurada.");

    private sealed record TenantSubscriptionWithPlan(SalesSaaS.Domain.SubscriptionStatus Status, DateTime ExpiresAtUtc, int MaxUsers, int MaxWarehouses, int MaxInvoicesPerMonth, bool SupportsAfip)
    {
        public SubscriptionPlanLimits SubscriptionPlan => new(MaxUsers, MaxWarehouses, MaxInvoicesPerMonth, SupportsAfip);
    }
    private sealed record SubscriptionPlanLimits(int MaxUsers, int MaxWarehouses, int MaxInvoicesPerMonth, bool SupportsAfip);
}
