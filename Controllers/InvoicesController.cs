using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using SalesSaaS.Domain;
using SalesSaaS.Features.Sales;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(Roles = Roles.Sales)]
public sealed class InvoicesController(ISender sender, ApplicationDbContext context) : ControllerBase
{
    [HttpPost("issue")]
    public async Task<ActionResult<InvoiceIssueResultDto>> Issue([FromBody] IssueInvoiceCommand command) => Ok(await sender.Send(command));

    [HttpGet("{invoiceId:guid}/pdf")]
    public async Task<IActionResult> DownloadPdf(Guid invoiceId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices.AsNoTracking()
            .Include(item => item.Customer)
            .Include(item => item.Order).ThenInclude(order => order!.Items).ThenInclude(item => item.Product)
            .SingleOrDefaultAsync(item => item.Id == invoiceId && item.TenantId == tenantId, cancellationToken);
        if (invoice is null) return NotFound();

        var tenant = await context.Tenants.AsNoTracking().SingleAsync(item => item.Id == tenantId, cancellationToken);
        var pdf = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10).FontColor(Colors.Grey.Darken3));
            page.Header().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(title => { title.Item().Text(tenant.LegalName ?? tenant.Name).Bold().FontSize(20).FontColor(Colors.Pink.Darken2); title.Item().Text(tenant.TaxId.Length > 0 ? $"CUIT: {tenant.TaxId}" : ""); });
                    row.ConstantItem(190).AlignRight().Column(data => { data.Item().Text(VoucherLabel(invoice.AfipVoucherType)).Bold().FontSize(16); data.Item().Text($"N.º {invoice.Number}"); data.Item().Text(invoice.IssuedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm")); });
                });
                column.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Pink.Lighten3);
            });
            page.Content().PaddingVertical(20).Column(column =>
            {
                column.Spacing(12);
                column.Item().Text("Cliente").Bold().FontSize(12);
                column.Item().Text(invoice.Customer?.LegalName is { Length: > 0 } legal ? legal : invoice.Customer?.Name ?? "Consumidor Final");
                column.Item().Text($"{invoice.Customer?.DocumentType}: {invoice.Customer?.DocumentNumber}");
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(4); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
                    table.Header(header => { header.Cell().Element(CellHeader).Text("Descripción"); header.Cell().Element(CellHeader).AlignRight().Text("Cant."); header.Cell().Element(CellHeader).AlignRight().Text("Unitario"); header.Cell().Element(CellHeader).AlignRight().Text("Importe"); });
                    foreach (var item in invoice.Order?.Items ?? [])
                    {
                        table.Cell().Element(CellBody).Text(item.Product?.Name ?? "Producto"); table.Cell().Element(CellBody).AlignRight().Text(item.Quantity.ToString()); table.Cell().Element(CellBody).AlignRight().Text(item.UnitPrice.ToString("C2", new System.Globalization.CultureInfo("es-AR"))); table.Cell().Element(CellBody).AlignRight().Text(item.SubTotal.ToString("C2", new System.Globalization.CultureInfo("es-AR")));
                    }
                });
                column.Item().AlignRight().PaddingTop(8).Text($"TOTAL  {invoice.TotalAmount.ToString("C2", new System.Globalization.CultureInfo("es-AR"))}").Bold().FontSize(16).FontColor(Colors.Pink.Darken2);
                if (!string.IsNullOrWhiteSpace(invoice.Cae)) column.Item().Background(Colors.Green.Lighten5).Padding(10).Text($"CAE: {invoice.Cae} · Vencimiento: {invoice.CaeExpirationDate:dd/MM/yyyy}");
                if (!string.IsNullOrWhiteSpace(invoice.Cae) && !string.IsNullOrWhiteSpace(invoice.BarCode))
                    column.Item().Width(100).Image(PngByteQRCodeHelper.GetQRCode(invoice.BarCode, QRCodeGenerator.ECCLevel.M, 6));
                if (string.IsNullOrWhiteSpace(invoice.Cae))
                    column.Item().Text(invoice.AfipVoucherType is null ? "DOCUMENTO NO FISCAL" : "PENDIENTE DE AUTORIZACIÓN — SIN VALIDEZ FISCAL").Bold().FontColor(Colors.Red.Darken2);
            });
            page.Footer().AlignCenter().Text("Comprobante generado por SalesSaaS").FontSize(8).FontColor(Colors.Grey.Darken1);
        })).GeneratePdf();
        return File(pdf, "application/pdf", $"{invoice.Number}.pdf");
    }

    private static string VoucherLabel(AfipVoucherType? type) => type switch { AfipVoucherType.InvoiceA => "FACTURA A", AfipVoucherType.InvoiceB => "FACTURA B", AfipVoucherType.InvoiceC => "FACTURA C", AfipVoucherType.CreditNoteA => "NOTA DE CRÉDITO A", AfipVoucherType.CreditNoteB => "NOTA DE CRÉDITO B", AfipVoucherType.CreditNoteC => "NOTA DE CRÉDITO C", _ => "TICKET / PRESUPUESTO" };
    private static IContainer CellHeader(IContainer container) => container.Background(Colors.Pink.Lighten4).Padding(6).DefaultTextStyle(style => style.SemiBold());
    private static IContainer CellBody(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(7).PaddingHorizontal(6);
}
