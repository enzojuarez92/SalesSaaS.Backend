using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

public sealed record AdminDataCountDto(string Label, long Count);
public sealed record AdminTenantUsageDto(string TenantName, long Products, long Customers, long Sales, long Invoices, decimal SalesVolume);
public sealed record AdminEndpointMetricDto(string Method, string Path, long Requests, long FailedRequests, long AverageDurationMs, long MaxDurationMs, DateTime LastOccurredAtUtc);
public sealed record AdminErrorLogDto(DateTime OccurredAtUtc, int StatusCode, string Method, string Path, string? TenantName, string? UserEmail, string ErrorType, string Message, string TraceId);
public sealed record AdminObservabilityDto(long DatabaseSizeBytes, IReadOnlyList<AdminDataCountDto> DataCounts, IReadOnlyList<AdminTenantUsageDto> TenantUsage, IReadOnlyList<AdminEndpointMetricDto> TopEndpoints, IReadOnlyList<AdminErrorLogDto> RecentErrors);

[ApiController]
[Route("api/admin/observability")]
[Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminObservabilityController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<AdminObservabilityDto> Get(CancellationToken cancellationToken)
    {
        var databaseSize = await GetDatabaseSizeAsync(cancellationToken);
        var dataCounts = new List<AdminDataCountDto>
        {
            new("Empresas", await context.Tenants.IgnoreQueryFilters().LongCountAsync(cancellationToken)),
            new("Usuarios", await context.Users.IgnoreQueryFilters().LongCountAsync(cancellationToken)),
            new("Productos", await context.Products.IgnoreQueryFilters().LongCountAsync(cancellationToken)),
            new("Clientes", await context.Customers.IgnoreQueryFilters().LongCountAsync(cancellationToken)),
            new("Ventas", await context.Orders.IgnoreQueryFilters().LongCountAsync(cancellationToken)),
            new("Comprobantes", await context.Invoices.IgnoreQueryFilters().LongCountAsync(cancellationToken)),
            new("Movimientos de stock", await context.StockMovements.IgnoreQueryFilters().LongCountAsync(cancellationToken)),
            new("Registros de auditoría", await context.AuditLogs.IgnoreQueryFilters().LongCountAsync(cancellationToken))
        };

        var tenants = await context.Tenants.IgnoreQueryFilters().AsNoTracking().Select(item => new { item.Id, item.Name }).ToListAsync(cancellationToken);
        var productCounts = await context.Products.IgnoreQueryFilters().AsNoTracking().GroupBy(item => item.TenantId).Select(group => new { TenantId = group.Key, Count = group.LongCount() }).ToDictionaryAsync(item => item.TenantId, item => item.Count, cancellationToken);
        var customerCounts = await context.Customers.IgnoreQueryFilters().AsNoTracking().GroupBy(item => item.TenantId).Select(group => new { TenantId = group.Key, Count = group.LongCount() }).ToDictionaryAsync(item => item.TenantId, item => item.Count, cancellationToken);
        var sales = await context.Orders.IgnoreQueryFilters().AsNoTracking().Where(item => item.Status != "Cancelled").GroupBy(item => item.TenantId).Select(group => new { TenantId = group.Key, Count = group.LongCount(), Total = group.Sum(item => item.TotalAmount) }).ToDictionaryAsync(item => item.TenantId, item => (item.Count, item.Total), cancellationToken);
        var invoices = await context.Invoices.IgnoreQueryFilters().AsNoTracking().GroupBy(item => item.TenantId).Select(group => new { TenantId = group.Key, Count = group.LongCount() }).ToDictionaryAsync(item => item.TenantId, item => item.Count, cancellationToken);
        var tenantUsage = tenants.Select(item =>
        {
            var tenantSales = sales.GetValueOrDefault(item.Id);
            return new AdminTenantUsageDto(item.Name, productCounts.GetValueOrDefault(item.Id), customerCounts.GetValueOrDefault(item.Id), tenantSales.Count, invoices.GetValueOrDefault(item.Id), tenantSales.Total);
        }).OrderByDescending(item => item.SalesVolume).ThenByDescending(item => item.Sales).Take(8).ToList();

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var endpointRows = await context.ApiEndpointMetrics.AsNoTracking().Where(item => item.LastOccurredAtUtc >= weekAgo)
            .GroupBy(item => new { item.Method, item.Path })
            .Select(group => new
            {
                group.Key.Method,
                group.Key.Path,
                Requests = group.Sum(item => item.RequestCount),
                FailedRequests = group.Sum(item => item.FailedRequestCount),
                TotalDuration = group.Sum(item => item.TotalDurationMs),
                MaxDuration = group.Max(item => item.MaxDurationMs),
                LastOccurredAtUtc = group.Max(item => item.LastOccurredAtUtc)
            }).OrderByDescending(item => item.Requests).Take(8).ToListAsync(cancellationToken);
        var endpoints = endpointRows.Select(item => new AdminEndpointMetricDto(item.Method, item.Path, item.Requests, item.FailedRequests, item.Requests == 0 ? 0 : item.TotalDuration / item.Requests, item.MaxDuration, item.LastOccurredAtUtc)).ToList();

        var errorRows = await (
            from error in context.PlatformErrorLogs.AsNoTracking()
            join tenant in context.Tenants.IgnoreQueryFilters().AsNoTracking() on error.TenantId equals (Guid?)tenant.Id into tenantsByError
            from tenant in tenantsByError.DefaultIfEmpty()
            orderby error.OccurredAtUtc descending
            select new AdminErrorLogDto(error.OccurredAtUtc, error.StatusCode, error.Method, error.Path, tenant == null ? null : tenant.Name, error.UserEmail, error.ErrorType, error.Message, error.TraceId)
        ).Take(12).ToListAsync(cancellationToken);

        return new AdminObservabilityDto(databaseSize, dataCounts, tenantUsage, endpoints, errorRows);
    }

    private async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(SUM(CAST(size AS BIGINT)) * 8192 AS BIGINT) FROM sys.database_files";
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null || value is DBNull ? 0 : Convert.ToInt64(value);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }
}
