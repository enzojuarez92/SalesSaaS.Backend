using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record AdminDashboardDto(int RegisteredTenants, int ActiveSubscriptions, int TrialingTenants, decimal EstimatedMonthlyRevenue);
public sealed record AdminTenantDto(Guid Id, string Name, string TaxId, string PlanName, Guid? SubscriptionPlanId, string Status, bool IsActive, DateTime CreatedAtUtc);
public sealed record AdminPagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);
public sealed record ExtendTrialRequest(int Days = 7);
public sealed record ChangeTenantPlanRequest(Guid SubscriptionPlanId);
public sealed record SetTenantAccessRequest(bool IsActive);

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<AdminDashboardDto> Dashboard(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var tenants = await context.Tenants.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        var subscriptions = await CurrentSubscriptions(cancellationToken);
        var subscriptionsByTenant = subscriptions.ToDictionary(item => item.TenantId);
        var active = tenants.Where(tenant => tenant.IsActive && subscriptionsByTenant.TryGetValue(tenant.Id, out var subscription)
            && subscription.Status == SubscriptionStatus.Active && subscription.ExpiresAtUtc > now).ToList();
        var trials = tenants.Count(tenant => tenant.IsActive && subscriptionsByTenant.TryGetValue(tenant.Id, out var subscription)
            && subscription.Status == SubscriptionStatus.Trialing && subscription.ExpiresAtUtc > now);

        return new AdminDashboardDto(tenants.Count, active.Count, trials,
            active.Sum(tenant => subscriptionsByTenant[tenant.Id].MonthlyPrice));
    }

    [HttpGet("tenants")]
    public async Task<AdminPagedResult<AdminTenantDto>> Tenants([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 15, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var tenants = await context.Tenants.IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            tenants = tenants.Where(tenant => tenant.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || tenant.TaxId.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        var subscriptions = (await CurrentSubscriptions(cancellationToken)).ToDictionary(item => item.TenantId);
        var totalCount = tenants.Count;
        var items = tenants.OrderByDescending(tenant => tenant.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(tenant => ToDto(tenant, subscriptions.GetValueOrDefault(tenant.Id))).ToList();
        return new AdminPagedResult<AdminTenantDto>(items, pageNumber, pageSize, totalCount, Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize)));
    }

    [HttpGet("plans")]
    public async Task<IReadOnlyList<AdminPlanDto>> Plans(CancellationToken cancellationToken) =>
        await context.SubscriptionPlans.IgnoreQueryFilters().AsNoTracking().Where(plan => plan.IsActive).OrderBy(plan => plan.MonthlyPrice)
            .Select(plan => new AdminPlanDto(plan.Id, plan.Name, plan.MonthlyPrice, plan.Currency)).ToListAsync(cancellationToken);

    [HttpPost("tenants/{tenantId:guid}/extend-trial")]
    public async Task<ActionResult<AdminTenantDto>> ExtendTrial(Guid tenantId, [FromBody] ExtendTrialRequest request, CancellationToken cancellationToken)
    {
        if (request.Days is < 1 or > 90) return BadRequest(new { message = "La extensión debe ser de 1 a 90 días." });
        var subscription = await LatestSubscription(tenantId, cancellationToken);
        if (subscription.Status != SubscriptionStatus.Trialing) return BadRequest(new { message = "Sólo se puede extender una suscripción en prueba." });
        subscription.ExpiresAtUtc = (subscription.ExpiresAtUtc > DateTime.UtcNow ? subscription.ExpiresAtUtc : DateTime.UtcNow).AddDays(request.Days);
        await context.SaveChangesAsync(cancellationToken);
        var tenant = await FindTenant(tenantId, cancellationToken);
        return Ok(ToDto(tenant, subscription));
    }

    [HttpPost("tenants/{tenantId:guid}/change-plan")]
    public async Task<ActionResult<AdminTenantDto>> ChangePlan(Guid tenantId, [FromBody] ChangeTenantPlanRequest request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == request.SubscriptionPlanId && item.IsActive, cancellationToken);
        if (plan is null) return BadRequest(new { message = "El plan seleccionado no está disponible." });
        await FindTenant(tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var subscription = new TenantSubscription { Id = Guid.NewGuid(), TenantId = tenantId, SubscriptionPlanId = plan.Id, SubscriptionPlan = plan, Status = SubscriptionStatus.Active, StartsAtUtc = now, ExpiresAtUtc = now.AddMonths(1), AutoRenew = false };
        context.TenantSubscriptions.Add(subscription);
        await context.SaveChangesAsync(cancellationToken);
        var tenant = await FindTenant(tenantId, cancellationToken);
        return Ok(ToDto(tenant, subscription));
    }

    [HttpPost("tenants/{tenantId:guid}/access")]
    public async Task<ActionResult<AdminTenantDto>> SetAccess(Guid tenantId, [FromBody] SetTenantAccessRequest request, CancellationToken cancellationToken)
    {
        var tenant = await FindTenant(tenantId, cancellationToken);
        tenant.IsActive = request.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        var subscription = await LatestSubscription(tenantId, cancellationToken);
        return Ok(ToDto(tenant, subscription));
    }

    private async Task<Tenant> FindTenant(Guid tenantId, CancellationToken cancellationToken) =>
        await context.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken)
        ?? throw new KeyNotFoundException("No encontramos la empresa solicitada.");

    private async Task<TenantSubscription> LatestSubscription(Guid tenantId, CancellationToken cancellationToken) =>
        await context.TenantSubscriptions.IgnoreQueryFilters().Include(subscription => subscription.SubscriptionPlan).Where(subscription => subscription.TenantId == tenantId)
            .OrderByDescending(subscription => (subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.Trialing) && subscription.ExpiresAtUtc > DateTime.UtcNow).ThenByDescending(subscription => subscription.StartsAtUtc).FirstOrDefaultAsync(cancellationToken)
        ?? throw new KeyNotFoundException("La empresa no tiene una suscripción configurada.");

    private async Task<List<SubscriptionSnapshot>> CurrentSubscriptions(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rows = await context.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking().Include(subscription => subscription.SubscriptionPlan)
            .OrderByDescending(subscription => subscription.StartsAtUtc).ToListAsync(cancellationToken);
        return rows.GroupBy(subscription => subscription.TenantId).Select(group => group.OrderByDescending(subscription => IsCurrent(subscription, now)).ThenByDescending(subscription => subscription.StartsAtUtc).First())
            .Select(subscription => new SubscriptionSnapshot(subscription.TenantId, subscription.SubscriptionPlanId, subscription.SubscriptionPlan?.Name ?? "Sin plan", subscription.SubscriptionPlan?.MonthlyPrice ?? 0m, subscription.Status, subscription.ExpiresAtUtc)).ToList();
    }

    private static bool IsCurrent(TenantSubscription subscription, DateTime? now = null) =>
        (subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing) && subscription.ExpiresAtUtc > (now ?? DateTime.UtcNow);

    private static AdminTenantDto ToDto(Tenant tenant, TenantSubscription? subscription) =>
        new(tenant.Id, tenant.Name, tenant.TaxId, subscription?.SubscriptionPlan?.Name ?? "Sin plan", subscription?.SubscriptionPlanId, TenantStatus(tenant, subscription?.Status), tenant.IsActive, tenant.CreatedAt);

    private static AdminTenantDto ToDto(Tenant tenant, SubscriptionSnapshot? subscription) =>
        new(tenant.Id, tenant.Name, tenant.TaxId, subscription?.PlanName ?? "Sin plan", subscription?.PlanId, TenantStatus(tenant, subscription?.Status), tenant.IsActive, tenant.CreatedAt);

    private static string TenantStatus(Tenant tenant, SubscriptionStatus? status) => !tenant.IsActive ? "Suspendida" : status switch { SubscriptionStatus.Active => "Activa", SubscriptionStatus.Trialing => "Trial", SubscriptionStatus.PastDue => "Pago pendiente", SubscriptionStatus.Canceled => "Cancelada", _ => "Sin suscripción" };

    private sealed record SubscriptionSnapshot(Guid TenantId, Guid PlanId, string PlanName, decimal MonthlyPrice, SubscriptionStatus Status, DateTime ExpiresAtUtc);
}

public sealed record AdminPlanDto(Guid Id, string Name, decimal MonthlyPrice, string Currency);
