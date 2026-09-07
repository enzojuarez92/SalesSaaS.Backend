using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Application.Reporting;
using SalesSaaS.Domain;
using SalesSaaS.Features.Analytics;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Roles = Roles.Administration)]
public sealed class AnalyticsController(IMediator mediator, IReportExportService reportExportService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<DashboardKpisDto> GetDashboard([FromQuery] Guid tenantId, [FromQuery] DateOnly? date) => await mediator.Send(new GetDashboardKpisQuery(tenantId, date));

    [HttpGet("products/top-selling")]
    public async Task<IReadOnlyList<TopSellingProductDto>> GetTopSelling([FromQuery] Guid tenantId, [FromQuery] int days = 30, [FromQuery] int take = 10) => await mediator.Send(new GetTopSellingProductsQuery(tenantId, days, take));

    [HttpGet("products/top-selling/export/csv")]
    public async Task<FileContentResult> ExportTopSellingCsv([FromQuery] Guid tenantId, [FromQuery] int days = 30, [FromQuery] int take = 10)
    {
        var report = await mediator.Send(new GetTopSellingProductsQuery(tenantId, days, take));
        return File(reportExportService.CreateCsv(report), "text/csv; charset=utf-8", "productos-mas-vendidos.csv");
    }

    [HttpGet("products/without-movement")]
    public async Task<IReadOnlyList<ProductWithoutMovementDto>> GetProductsWithoutMovement([FromQuery] Guid tenantId) => await mediator.Send(new GetProductsWithoutMovementQuery(tenantId));

    [HttpGet("inventory/low-stock")]
    public async Task<IReadOnlyList<LowStockProductDto>> GetLowStock([FromQuery] Guid tenantId) => await mediator.Send(new GetLowStockProductsQuery(tenantId));

    [HttpGet("inventory/valuation")]
    public async Task<IReadOnlyList<WarehouseInventoryValuationDto>> GetInventoryValuation([FromQuery] Guid tenantId) => await mediator.Send(new GetInventoryValuationQuery(tenantId));
}
