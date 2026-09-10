using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Analytics;

public sealed record GetDashboardKpisQuery(Guid TenantId, DateOnly? Date = null) : IRequest<DashboardKpisDto>, ITenantScopedRequest;
public sealed record GetTopSellingProductsQuery(Guid TenantId, int Days = 30, int Take = 10) : IRequest<IReadOnlyList<TopSellingProductDto>>, ITenantScopedRequest;
public sealed record GetProductsWithoutMovementQuery(Guid TenantId) : IRequest<IReadOnlyList<ProductWithoutMovementDto>>, ITenantScopedRequest;
public sealed record GetLowStockProductsQuery(Guid TenantId) : IRequest<IReadOnlyList<LowStockProductDto>>, ITenantScopedRequest;
public sealed record GetInventoryValuationQuery(Guid TenantId) : IRequest<IReadOnlyList<WarehouseInventoryValuationDto>>, ITenantScopedRequest;
public sealed record GetDashboardSummaryQuery(Guid TenantId) : IRequest<DashboardSummaryDto>, ITenantScopedRequest;
public sealed record GetDashboardSalesChartQuery(Guid TenantId, int Days = 30) : IRequest<IReadOnlyList<DashboardSalesChartPointDto>>, ITenantScopedRequest;

public sealed record PeriodMetricDto(decimal Current, decimal Previous, decimal VariationPercentage);
public sealed record DashboardKpisDto(PeriodMetricDto DailySales, PeriodMetricDto MonthlySales, decimal EstimatedGrossProfit, decimal EstimatedGrossMarginPercentage, int ProcessedOrders, decimal AverageTicket, decimal TotalReceivable, decimal TotalPayable);
public sealed record TopSellingProductDto(Guid ProductId, string Sku, string Name, int QuantitySold, decimal Revenue);
public sealed record ProductWithoutMovementDto(Guid ProductId, string Sku, string Name, int Stock);
public sealed record LowStockProductDto(Guid ProductId, string Sku, string Name, int CurrentStock, int MinimumStockAlert);
public sealed record WarehouseInventoryValuationDto(Guid WarehouseId, string WarehouseName, int Units, decimal Valuation);
public sealed record DashboardCashDto(Guid Id, string WarehouseName, decimal ExpectedCash, DateTime OpenedAtUtc);
public sealed record RecentSaleDto(Guid Id, string CustomerName, decimal TotalAmount, string Status, DateTime OccurredAtUtc, PaymentMethod PaymentMethod);
public sealed record DashboardSummaryDto(decimal DailySales, int DailyTransactions, decimal MonthlySales, int MonthlyTransactions, decimal TotalReceivable, int CriticalStockCount, DashboardCashDto? CurrentCash, IReadOnlyList<RecentSaleDto> RecentSales);
public sealed record DashboardSalesChartPointDto(DateOnly Date, decimal Total, int Transactions);

public sealed class GetDashboardKpisQueryValidator : AbstractValidator<GetDashboardKpisQuery>
{
    public GetDashboardKpisQueryValidator() => RuleFor(query => query.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
}
public sealed class GetTopSellingProductsQueryValidator : AbstractValidator<GetTopSellingProductsQuery>
{
    public GetTopSellingProductsQueryValidator()
    {
        RuleFor(query => query.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(query => query.Days).InclusiveBetween(1, 366).WithMessage("El período debe estar entre 1 y 366 días.");
        RuleFor(query => query.Take).InclusiveBetween(1, 100).WithMessage("La cantidad solicitada debe estar entre 1 y 100.");
    }
}
public sealed class GetProductsWithoutMovementQueryValidator : AbstractValidator<GetProductsWithoutMovementQuery> { public GetProductsWithoutMovementQueryValidator() => RuleFor(query => query.TenantId).NotEmpty().WithMessage("El negocio es obligatorio."); }
public sealed class GetLowStockProductsQueryValidator : AbstractValidator<GetLowStockProductsQuery> { public GetLowStockProductsQueryValidator() => RuleFor(query => query.TenantId).NotEmpty().WithMessage("El negocio es obligatorio."); }
public sealed class GetInventoryValuationQueryValidator : AbstractValidator<GetInventoryValuationQuery> { public GetInventoryValuationQueryValidator() => RuleFor(query => query.TenantId).NotEmpty().WithMessage("El negocio es obligatorio."); }

public sealed class GetDashboardKpisQueryHandler(ApplicationDbContext context) : IRequestHandler<GetDashboardKpisQuery, DashboardKpisDto>
{
    public async Task<DashboardKpisDto> Handle(GetDashboardKpisQuery request, CancellationToken cancellationToken)
    {
        var day = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var monthStart = new DateTime(day.Year, day.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextDay = dayStart.AddDays(1);
        var nextMonth = monthStart.AddMonths(1);
        var previousDayStart = dayStart.AddDays(-1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var orders = context.Orders.AsNoTracking().Where(order => order.TenantId == request.TenantId && order.Status != "Cancelled");
        var dailySales = await SumOrders(orders, dayStart, nextDay, cancellationToken);
        var previousDailySales = await SumOrders(orders, previousDayStart, dayStart, cancellationToken);
        var monthlySales = await SumOrders(orders, monthStart, nextMonth, cancellationToken);
        var previousMonthlySales = await SumOrders(orders, previousMonthStart, monthStart, cancellationToken);
        var monthOrders = orders.Where(order => order.OrderDate >= monthStart && order.OrderDate < nextMonth);
        var processedOrders = await monthOrders.CountAsync(cancellationToken);
        var averageTicket = processedOrders == 0 ? 0 : monthlySales / processedOrders;
        var profit = await context.OrderItems.AsNoTracking().Where(item => item.Order!.TenantId == request.TenantId && item.Order.Status != "Cancelled" && item.Order.OrderDate >= monthStart && item.Order.OrderDate < nextMonth).SumAsync(item => (decimal?)((item.UnitPrice - item.Product!.Cost) * item.Quantity), cancellationToken) ?? 0;
        var receivable = await context.CustomerAccountEntries.AsNoTracking().Where(entry => entry.TenantId == request.TenantId).SumAsync(entry => (decimal?)(entry.Type == CustomerAccountEntryType.Debit ? entry.Amount : -entry.Amount), cancellationToken) ?? 0;
        var payable = await context.SupplierAccountEntries.AsNoTracking().Where(entry => entry.TenantId == request.TenantId).SumAsync(entry => (decimal?)(entry.IsDebit ? -entry.Amount : entry.Amount), cancellationToken) ?? 0;
        return new DashboardKpisDto(CreateMetric(dailySales, previousDailySales), CreateMetric(monthlySales, previousMonthlySales), profit, monthlySales == 0 ? 0 : decimal.Round(profit / monthlySales * 100, 2), processedOrders, decimal.Round(averageTicket, 2), receivable, payable);
    }

    private static async Task<decimal> SumOrders(IQueryable<Order> orders, DateTime start, DateTime end, CancellationToken cancellationToken) =>
        await orders.Where(order => order.OrderDate >= start && order.OrderDate < end).SumAsync(order => (decimal?)order.TotalAmount, cancellationToken) ?? 0;
    private static PeriodMetricDto CreateMetric(decimal current, decimal previous) => new(current, previous, previous == 0 ? 0 : decimal.Round((current - previous) / previous * 100, 2));
}

public sealed class GetTopSellingProductsQueryHandler(ApplicationDbContext context) : IRequestHandler<GetTopSellingProductsQuery, IReadOnlyList<TopSellingProductDto>>
{
    public async Task<IReadOnlyList<TopSellingProductDto>> Handle(GetTopSellingProductsQuery request, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow.Date.AddDays(-request.Days);
        return await context.OrderItems.AsNoTracking().Where(item => item.Order!.TenantId == request.TenantId && item.Order.Status != "Cancelled" && item.Order.OrderDate >= start)
            .GroupBy(item => new { item.ProductId, item.Product!.Sku, item.Product.Name })
            .OrderByDescending(group => group.Sum(item => item.Quantity)).ThenBy(group => group.Key.Name).Take(request.Take)
            .Select(group => new TopSellingProductDto(group.Key.ProductId, group.Key.Sku, group.Key.Name, group.Sum(item => item.Quantity), group.Sum(item => item.SubTotal))).ToListAsync(cancellationToken);
    }
}

public sealed class GetProductsWithoutMovementQueryHandler(ApplicationDbContext context) : IRequestHandler<GetProductsWithoutMovementQuery, IReadOnlyList<ProductWithoutMovementDto>>
{
    public async Task<IReadOnlyList<ProductWithoutMovementDto>> Handle(GetProductsWithoutMovementQuery request, CancellationToken cancellationToken) =>
        await context.Products.AsNoTracking().Where(product => product.TenantId == request.TenantId && product.IsActive && !context.StockMovements.Any(movement => movement.ProductId == product.Id))
            .OrderBy(product => product.Name).Select(product => new ProductWithoutMovementDto(product.Id, product.Sku, product.Name, product.Stock)).ToListAsync(cancellationToken);
}

public sealed class GetLowStockProductsQueryHandler(ApplicationDbContext context) : IRequestHandler<GetLowStockProductsQuery, IReadOnlyList<LowStockProductDto>>
{
    public async Task<IReadOnlyList<LowStockProductDto>> Handle(GetLowStockProductsQuery request, CancellationToken cancellationToken) =>
        await context.Products.AsNoTracking().Where(product => product.TenantId == request.TenantId && product.IsActive && product.Stock <= product.MinimumStockAlert)
            .OrderBy(product => product.Stock).ThenBy(product => product.Name).Select(product => new LowStockProductDto(product.Id, product.Sku, product.Name, product.Stock, product.MinimumStockAlert)).ToListAsync(cancellationToken);
}

public sealed class GetInventoryValuationQueryHandler(ApplicationDbContext context) : IRequestHandler<GetInventoryValuationQuery, IReadOnlyList<WarehouseInventoryValuationDto>>
{
    public async Task<IReadOnlyList<WarehouseInventoryValuationDto>> Handle(GetInventoryValuationQuery request, CancellationToken cancellationToken) =>
        await context.StockMovements.AsNoTracking().Where(movement => movement.TenantId == request.TenantId)
            .GroupBy(movement => new { movement.WarehouseId, movement.Warehouse!.Name })
            .Select(group => new WarehouseInventoryValuationDto(group.Key.WarehouseId, group.Key.Name, group.Sum(movement => movement.Quantity), group.Sum(movement => movement.Quantity * movement.Product!.Cost)))
            .OrderBy(item => item.WarehouseName).ToListAsync(cancellationToken);
}

public sealed class GetDashboardSummaryQueryHandler(ApplicationDbContext context, ISender sender) : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var kpis = await sender.Send(new GetDashboardKpisQuery(request.TenantId), cancellationToken);
        var today = DateTime.UtcNow.Date;
        var dailyTransactions = await context.Orders.AsNoTracking().CountAsync(order => order.TenantId == request.TenantId && order.Status != "Cancelled" && order.OrderDate >= today && order.OrderDate < today.AddDays(1), cancellationToken);
        var criticalStock = await context.Products.AsNoTracking().CountAsync(product => product.TenantId == request.TenantId && product.IsActive && product.Stock <= product.MinimumStockAlert, cancellationToken);
        var cashSession = await context.CashRegisterSessions.AsNoTracking().Where(session => session.TenantId == request.TenantId && session.Status == "Open").OrderByDescending(session => session.OpenedAtUtc).FirstOrDefaultAsync(cancellationToken);
        DashboardCashDto? cash = null;
        if (cashSession is not null)
        {
            var cashNet = await context.CashMovements.AsNoTracking().Where(movement => movement.CashRegisterSessionId == cashSession.Id && movement.PaymentMethod == PaymentMethod.Cash).SumAsync(movement => (decimal?)(movement.IsIncome ? movement.Amount : -movement.Amount), cancellationToken) ?? 0;
            var warehouseName = await context.Warehouses.AsNoTracking().Where(warehouse => warehouse.Id == cashSession.WarehouseId).Select(warehouse => warehouse.Name).SingleOrDefaultAsync(cancellationToken) ?? "Depósito";
            cash = new DashboardCashDto(cashSession.Id, warehouseName, cashSession.OpeningBalance + cashNet, cashSession.OpenedAtUtc);
        }
        var recentSales = await context.Orders.AsNoTracking().Where(order => order.TenantId == request.TenantId).OrderByDescending(order => order.OrderDate).Take(6)
            .Select(order => new RecentSaleDto(order.Id, order.Customer!.Name, order.TotalAmount, order.Status, order.OrderDate, order.PaymentMethod)).ToListAsync(cancellationToken);
        return new DashboardSummaryDto(kpis.DailySales.Current, dailyTransactions, kpis.MonthlySales.Current, kpis.ProcessedOrders, Math.Max(0, kpis.TotalReceivable), criticalStock, cash, recentSales);
    }
}

public sealed class GetDashboardSalesChartQueryHandler(ApplicationDbContext context) : IRequestHandler<GetDashboardSalesChartQuery, IReadOnlyList<DashboardSalesChartPointDto>>
{
    public async Task<IReadOnlyList<DashboardSalesChartPointDto>> Handle(GetDashboardSalesChartQuery request, CancellationToken cancellationToken)
    {
        var days = Math.Clamp(request.Days, 1, 90);
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var totals = await context.Orders.AsNoTracking().Where(order => order.TenantId == request.TenantId && order.Status != "Cancelled" && order.OrderDate >= start)
            .GroupBy(order => order.OrderDate.Date).Select(group => new { Date = group.Key, Total = group.Sum(order => order.TotalAmount), Transactions = group.Count() }).ToListAsync(cancellationToken);
        var byDate = totals.ToDictionary(item => DateOnly.FromDateTime(item.Date));
        return Enumerable.Range(0, days).Select(offset =>
        {
            var date = DateOnly.FromDateTime(start.AddDays(offset));
            return byDate.TryGetValue(date, out var item) ? new DashboardSalesChartPointDto(date, item.Total, item.Transactions) : new DashboardSalesChartPointDto(date, 0, 0);
        }).ToList();
    }
}
