using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Security;

public static class SuperAdminSeeder
{
    public static async Task EnsureConfiguredSuperAdminsAsync(ApplicationDbContext context, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var emails = configuration.GetSection("SuperAdmin:Emails").Get<string[]>()
            ?.Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        if (emails.Length == 0) return;

        var memberships = await context.TenantMemberships.IgnoreQueryFilters().Include(membership => membership.User)
            .Where(membership => membership.User != null && emails.Contains(membership.User.Email))
            .OrderBy(membership => membership.CreatedAt).ToListAsync(cancellationToken);
        foreach (var membership in memberships.GroupBy(item => item.UserId).Select(group => group.First()))
        {
            if (membership.Role == Roles.SuperAdmin) continue;
            membership.Role = Roles.SuperAdmin;
            membership.User!.TokenVersion++;
        }
        if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
    }
}
