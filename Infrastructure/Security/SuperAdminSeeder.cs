using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Security;

public static class SuperAdminSeeder
{
    public static async Task EnsureConfiguredSuperAdminsAsync(ApplicationDbContext context, IConfiguration configuration, IPasswordHasher<User> passwordHasher, CancellationToken cancellationToken = default)
    {
        await EnsureBootstrapUserAsync(context, configuration, passwordHasher, cancellationToken);
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

    private static async Task EnsureBootstrapUserAsync(ApplicationDbContext context, IConfiguration configuration, IPasswordHasher<User> passwordHasher, CancellationToken cancellationToken)
    {
        var email = configuration["SuperAdmin:Bootstrap:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["SuperAdmin:Bootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var user = await context.Users.IgnoreQueryFilters().SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        if (user is null)
        {
            var tenant = await context.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(candidate => candidate.Name == "SalesSaaS Platform", cancellationToken)
                ?? new Tenant { Id = Guid.NewGuid(), Name = "SalesSaaS Platform", TaxId = "PLATFORM", LegalName = "Administración de SalesSaaS", IsActive = true };
            if (tenant.Id == Guid.Empty) tenant.Id = Guid.NewGuid();
            user = new User { Id = Guid.NewGuid(), FirstName = "Super", LastName = "Administrador", Email = email, IsActive = true };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            context.Users.Add(user);
            if (context.Entry(tenant).State == EntityState.Detached) context.Tenants.Add(tenant);
            context.TenantMemberships.Add(new TenantMembership { Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenant.Id, Role = Roles.SuperAdmin, IsActive = true });
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        var membership = await context.TenantMemberships.IgnoreQueryFilters().Where(candidate => candidate.UserId == user.Id).OrderBy(candidate => candidate.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (membership is null) throw new InvalidOperationException("El correo configurado como SuperAdmin no tiene una membresía asociada.");
        if (membership.Role != Roles.SuperAdmin)
        {
            membership.Role = Roles.SuperAdmin;
            user.TokenVersion++;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
