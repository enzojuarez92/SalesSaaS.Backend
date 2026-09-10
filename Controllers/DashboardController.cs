using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.Analytics;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.Sales)]
public sealed class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<DashboardSummaryDto> Summary([FromQuery] Guid tenantId, [FromQuery] Guid? warehouseId) => await sender.Send(new GetDashboardSummaryQuery(tenantId, warehouseId));

    [HttpGet("top-products")]
    public async Task<IReadOnlyList<TopSellingProductDto>> TopProducts([FromQuery] Guid tenantId, [FromQuery] Guid? warehouseId) => await sender.Send(new GetTopSellingProductsQuery(tenantId, 30, 5, warehouseId));

    [HttpGet("sales-chart")]
    public async Task<IReadOnlyList<DashboardSalesChartPointDto>> SalesChart([FromQuery] Guid tenantId, [FromQuery] int days = 30, [FromQuery] Guid? warehouseId = null) => await sender.Send(new GetDashboardSalesChartQuery(tenantId, days, warehouseId));
}
