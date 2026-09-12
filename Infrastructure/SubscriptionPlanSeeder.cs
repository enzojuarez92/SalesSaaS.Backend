using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure;

public static class SubscriptionPlanSeeder
{
    public static async Task EnsurePlansAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var plans = new[]
        {
            new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Starter", MonthlyPrice = 0, AnnualPrice = 0, Currency = "ARS", MaxUsers = 3, MaxWarehouses = 1, MaxInvoicesPerMonth = 25, SupportsAfip = false, IsDefault = !await context.SubscriptionPlans.AnyAsync(plan => plan.IsDefault, cancellationToken) },
            new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Pro", MonthlyPrice = 35000, AnnualPrice = 350000, Currency = "ARS", MaxUsers = 10, MaxWarehouses = 5, MaxInvoicesPerMonth = 1000, SupportsAfip = true },
            new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Enterprise", MonthlyPrice = 79990, AnnualPrice = 799900, Currency = "ARS", MaxUsers = 100, MaxWarehouses = 50, MaxInvoicesPerMonth = 100000, SupportsAfip = true }
        };
        foreach (var plan in plans)
        {
            var existing = await context.SubscriptionPlans.SingleOrDefaultAsync(item => item.Name == plan.Name, cancellationToken);
            if (existing is null) context.SubscriptionPlans.Add(plan);
            else if (plan.Name == "Pro") { existing.MonthlyPrice = 35000m; existing.AnnualPrice = 350000m; }
        }
        var activeLegacyTrials = await context.TenantSubscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Trialing && subscription.ExpiresAtUtc > DateTime.UtcNow && subscription.ExpiresAtUtc > subscription.StartsAtUtc.AddDays(7))
            .ToListAsync(cancellationToken);
        foreach (var subscription in activeLegacyTrials) subscription.ExpiresAtUtc = subscription.StartsAtUtc.AddDays(7);
        if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
    }
}
