using SalesSaaS.Application.Billing;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Infrastructure.Billing;

public sealed class SubscriptionGatekeeperMiddleware(RequestDelegate next)
{
    private static readonly PathString[] ExcludedPaths = ["/api/profile", "/api/auth", "/api/billing", "/api/subscription", "/api/subscriptions", "/api/tenants", "/api/settings", "/swagger"];

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, ISubscriptionGatekeeper gatekeeper)
    {
        if (currentUser.IsAuthenticated && currentUser.TenantId.HasValue && !ExcludedPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
            await gatekeeper.EnsureActiveSubscriptionAsync(currentUser.TenantId.Value, context.RequestAborted);
        await next(context);
    }
}
