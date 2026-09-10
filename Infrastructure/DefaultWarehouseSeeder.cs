using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure;

public static class DefaultWarehouseSeeder
{
    public static async Task EnsureActiveWarehouseForEveryTenantAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters().Select(tenant => tenant.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
        {
            var warehouses = await context.Warehouses.IgnoreQueryFilters().Where(warehouse => warehouse.TenantId == tenantId).Select(warehouse => new { warehouse.Code, warehouse.IsActive }).ToListAsync(cancellationToken);
            if (warehouses.Any(warehouse => warehouse.IsActive)) continue;
            var code = "MAIN";
            var suffix = 1;
            while (warehouses.Any(warehouse => warehouse.Code == code)) code = $"MAIN-{++suffix}";
            context.Warehouses.Add(new Warehouse { Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = "Depósito Principal", IsActive = true });
        }
        if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
    }
}
