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
            new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Pro", MonthlyPrice = 24990, AnnualPrice = 249900, Currency = "ARS", MaxUsers = 10, MaxWarehouses = 5, MaxInvoicesPerMonth = 1000, SupportsAfip = true },
            new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Enterprise", MonthlyPrice = 79990, AnnualPrice = 799900, Currency = "ARS", MaxUsers = 100, MaxWarehouses = 50, MaxInvoicesPerMonth = 100000, SupportsAfip = true }
        };
        foreach (var plan in plans) if (!await context.SubscriptionPlans.AnyAsync(item => item.Name == plan.Name, cancellationToken)) context.SubscriptionPlans.Add(plan);
        if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
    }
}
