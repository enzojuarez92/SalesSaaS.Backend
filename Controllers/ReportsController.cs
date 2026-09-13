using MediatR;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Reporting;
using SalesSaaS.Domain;
using SalesSaaS.Features.Auditing;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;
public sealed record SalesReportRow(Guid Id, DateTime Date, string Customer, decimal Total, PaymentMethod PaymentMethod, string Status);
[ApiController][Route("api/reports")][Authorize(Roles=Roles.Administration)]
public sealed class ReportsController(ApplicationDbContext context, ISender sender) : ControllerBase
{
 [HttpGet("sales-summary")] public async Task<IReadOnlyList<SalesReportRow>> Sales([FromQuery]Guid tenantId,[FromQuery]DateTime? fromUtc,[FromQuery]DateTime? toUtc,[FromQuery]string? customer,[FromQuery]PaymentMethod? paymentMethod,[FromQuery]string? status,[FromQuery]Guid? warehouseId)=>await Query(tenantId,fromUtc,toUtc,customer,paymentMethod,status,warehouseId).ToListAsync();
 [HttpGet("sales/export-excel")]
 public async Task<FileContentResult> Export([FromQuery]Guid tenantId,[FromQuery]DateTime? fromUtc,[FromQuery]DateTime? toUtc,[FromQuery]string? customer,[FromQuery]PaymentMethod? paymentMethod,[FromQuery]string? status,[FromQuery]Guid? warehouseId)
 {
     var rows = await Query(tenantId, fromUtc, toUtc, customer, paymentMethod, status, warehouseId).ToListAsync();
     using var workbook = new XLWorkbook();
     var sheet = workbook.Worksheets.Add("Ventas");
     sheet.Cell("A1").Value = "Reporte de ventas";
     sheet.Range("A1:F1").Merge().Style.Font.SetBold().Font.SetFontSize(16).Fill.SetBackgroundColor(XLColor.FromHtml("#EC4899")).Font.SetFontColor(XLColor.White);
     sheet.Cell("A2").Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
     sheet.Cell("A4").InsertTable(rows.Select(row => new { Fecha = row.Date.ToLocalTime(), Cliente = row.Customer, Total = row.Total, MedioDePago = PaymentLabel(row.PaymentMethod), Estado = row.Status }), "Ventas", true);
     var table = sheet.Table("Ventas");
     table.Theme = XLTableTheme.TableStyleMedium2;
     sheet.Column(1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
     sheet.Column(3).Style.NumberFormat.Format = "$ #,##0.00";
     sheet.Columns().AdjustToContents();
     sheet.Column(2).Width = Math.Max(sheet.Column(2).Width, 24);
     using var stream = new MemoryStream();
     workbook.SaveAs(stream);
     return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ventas-{DateTime.UtcNow:yyyyMMdd}.xlsx");
 }
 [HttpGet("inventory-valuation")]
 public async Task<object> Inventory([FromQuery]Guid tenantId,[FromQuery]Guid? warehouseId)
 {
     var balances = context.StockMovements.Where(movement => movement.TenantId == tenantId && (!warehouseId.HasValue || movement.WarehouseId == warehouseId))
         .GroupBy(movement => movement.ProductId).Select(group => new { ProductId = group.Key, Quantity = group.Sum(movement => movement.Quantity) });
     var valuation = await context.Products.Where(product => product.TenantId == tenantId && product.IsActive)
         .Select(product => new { product.Cost, product.Price, Quantity = balances.Where(balance => balance.ProductId == product.Id).Select(balance => (int?)balance.Quantity).FirstOrDefault() ?? 0 })
         .ToListAsync();
     return new { cost = valuation.Sum(item => item.Quantity * item.Cost), retail = valuation.Sum(item => item.Quantity * item.Price) };
 }
 [HttpGet("audit-logs")] public async Task<IReadOnlyList<AuditLogDto>> Audit([FromQuery]Guid tenantId,[FromQuery]DateTime? fromUtc,[FromQuery]DateTime? toUtc,[FromQuery]Guid? warehouseId)=>await sender.Send(new GetAuditLogsQuery(tenantId,null,fromUtc,toUtc,100,warehouseId));
 private IQueryable<SalesReportRow> Query(Guid t,DateTime? f,DateTime? to,string? c,PaymentMethod? p,string? s,Guid? warehouseId){var q=context.Orders.AsNoTracking().Include(x=>x.Customer).Where(x=>x.TenantId==t&&(!warehouseId.HasValue||x.WarehouseId==warehouseId));if(f.HasValue)q=q.Where(x=>x.OrderDate>=f);if(to.HasValue)q=q.Where(x=>x.OrderDate<=to);if(!string.IsNullOrWhiteSpace(c))q=q.Where(x=>x.Customer!.Name.Contains(c));if(p.HasValue)q=q.Where(x=>x.PaymentMethod==p);if(!string.IsNullOrWhiteSpace(s))q=q.Where(x=>x.Status==s);return q.OrderByDescending(x=>x.OrderDate).Select(x=>new SalesReportRow(x.Id,x.OrderDate,x.Customer!.Name,x.TotalAmount,x.PaymentMethod,x.Status));}
 private static string PaymentLabel(PaymentMethod method) => method switch { PaymentMethod.Cash => "Efectivo", PaymentMethod.CreditCard => "Tarjeta de crédito", PaymentMethod.DebitCard => "Tarjeta de débito", PaymentMethod.BankTransfer => "Transferencia", PaymentMethod.MercadoPago => "Mercado Pago", PaymentMethod.VirtualWallet => "Billetera virtual", PaymentMethod.Account => "Cuenta corriente", PaymentMethod.Other => "Otros", _ => method.ToString() };
}
