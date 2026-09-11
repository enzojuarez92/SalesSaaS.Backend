using System.Globalization;
using System.IO.Compression;
using ClosedXML.Excel;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Features.Products.Commands;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

[ApiController, Route("api/inventory-tools"), Authorize]
public sealed class InventoryToolsController(ApplicationDbContext db, ICurrentUser current, ISender sender, IValidator<CreateProductCommand> validator) : ControllerBase
{
    private const string Mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly string[] Headers = ["SKU", "Nombre", "Descripción", "Precio", "Costo", "Stock inicial", "Stock mínimo", "Categoría"];
    private Guid Warehouse => current.WarehouseId ?? throw new InvalidOperationException("Seleccioná un depósito.");

    [HttpGet("products/{id:guid}/kardex")]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IActionResult> Kardex(Guid id, CancellationToken ct, int page = 1, int pageSize = 50)
    {
        var warehouseId = Warehouse;
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest(new { message = "Paginación inválida." });
        if (!await db.Products.AnyAsync(p => p.Id == id, ct)) return NotFound();
        var query = db.StockMovements.AsNoTracking().Where(m => m.ProductId == id && m.WarehouseId == warehouseId);
        // Build the running balance before paging; timestamp + ID give a stable order.
        var ledger = await query.OrderBy(m => m.OccurredAtUtc).ThenBy(m => m.CreatedAtUtc).ThenBy(m => m.Id).ToListAsync(ct);
        var userIds = ledger.Where(m => m.UserId.HasValue).Select(m => m.UserId!.Value).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FirstName + " " + u.LastName, ct);
        var warehouseName = await db.Warehouses.Where(w => w.Id == warehouseId).Select(w => w.Name).SingleAsync(ct);
        long balance = 0;
        var rows = ledger.Select(m => { var before = balance; balance += m.Quantity; return new { m.Id, m.OccurredAtUtc, m.Type, m.Quantity, StockBefore = before, StockAfter = balance, m.Reason, m.Reference, Warehouse = warehouseName, User = m.UserId.HasValue ? names.GetValueOrDefault(m.UserId.Value, "Usuario eliminado") : "Sin registrar (histórico)" }; }).ToList();
        return Ok(new { items = rows.AsEnumerable().Reverse().Skip((page - 1) * pageSize).Take(pageSize), totalCount = rows.Count, balance });
    }

    [HttpGet("products/template")]
    [Authorize(Roles = Roles.Inventory)]
    public IActionResult Template()
    {
        using var book = new XLWorkbook(); var sheet = book.AddWorksheet("Productos");
        for (var i = 0; i < Headers.Length; i++) sheet.Cell(1, i + 1).Value = Headers[i];
        sheet.Cell(2, 1).Value = "EJEMPLO-001"; sheet.Cell(2, 2).Value = "Producto de ejemplo";
        sheet.Cell(2, 4).Value = 1000m; sheet.Cell(2, 5).Value = 500m; sheet.Cell(2, 6).Value = 0; sheet.Cell(2, 7).Value = 5;
        book.AddWorksheet("Instrucciones").Cell(1, 1).Value = "Reemplazá la fila de ejemplo. Máximo 1000 productos. Categoría: nombre existente u opcional. Stock inicial en sucursal activa. Precios numéricos > 0. Si hay errores no se importa ninguna fila.";
        return Download(book, "Plantilla_Productos.xlsx");
    }

    [HttpGet("export/{kind}")]
    public async Task<IActionResult> Export(string kind, CancellationToken ct)
    {
        var warehouse = Warehouse;
        var roles = kind == "accounts" ? Roles.Sales : Roles.Inventory;
        if (!roles.Split(',').Any(User.IsInRole)) return Forbid();
        using var book = new XLWorkbook(); var sheet = book.AddWorksheet("Detalle");
        if (kind is "products" or "inventory")
        {
            var rows = await db.Products.AsNoTracking().OrderBy(p => p.Name).Select(p => new { p.Sku, p.Name, Category = db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).FirstOrDefault() ?? "Sin categoría", p.Cost, p.Price, Stock = db.StockMovements.Where(m => m.ProductId == p.Id && m.WarehouseId == warehouse).Sum(m => (int?)m.Quantity) ?? 0, p.MinimumStockAlert }).ToListAsync(ct);
            sheet.Cell(1, 1).InsertTable(rows.Select(p => new { p.Sku, Nombre = p.Name, Categoria = p.Category, Costo = p.Cost, Precio = p.Price, p.Stock, Minimo = p.MinimumStockAlert, Capital = p.Cost * p.Stock, ValorVenta = p.Price * p.Stock }));
            sheet.Columns(4, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Columns(8, 9).Style.NumberFormat.Format = "#,##0.00";
        }
        else if (kind == "accounts")
        {
            var rows = await db.CustomerAccountEntries.AsNoTracking().GroupBy(e => new { e.CustomerId, e.Customer!.Name, e.Customer.DocumentNumber }).Select(g => new { g.Key.Name, Documento = g.Key.DocumentNumber, Saldo = g.Sum(e => e.Type == CustomerAccountEntryType.Debit ? e.Amount : -e.Amount) }).ToListAsync(ct);
            sheet.Cell(1, 1).InsertTable(rows); sheet.Column(3).Style.NumberFormat.Format = "#,##0.00";
        }
        else return BadRequest(new { message = "Exportación desconocida." });
        return Download(book, $"{kind}_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    [HttpPost("products/import"), RequestSizeLimit(5 * 1024 * 1024)]
    [Authorize(Roles = Roles.Inventory)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        var warehouse = Warehouse;
        if (file.Length == 0 || file.Length > 5 * 1024 * 1024 || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Seleccioná un archivo .xlsx de hasta 5 MB." });
        using var stream = new MemoryStream(); await file.CopyToAsync(stream, ct); stream.Position = 0;
        try
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
                if (zip.Entries.Count > 1000 || zip.Entries.Sum(e => e.Length) > 50 * 1024 * 1024)
                    return BadRequest(new { message = "El archivo supera el tamaño descomprimido permitido." });
            stream.Position = 0;
            using var book = new XLWorkbook(stream); var sheet = book.Worksheets.First();
            if (!Headers.Select((h, i) => sheet.Cell(1, i + 1).GetString().Trim() == h).All(x => x))
                return BadRequest(new { message = "Las columnas no coinciden con la plantilla." });
            var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
            if (last is < 2 or > 1001) return BadRequest(new { message = "El archivo debe contener entre 1 y 1000 productos." });
            var existing = (await db.Products.Select(p => p.Sku).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var categories = await db.Categories.Where(c => c.IsActive).ToListAsync(ct);
            var pending = new List<CreateProductCommand>(); var errors = new List<object>();
            for (var row = 2; row <= last; row++)
            {
                if (sheet.Row(row).IsEmpty()) continue;
                try
                {
                    string Text(int col) => sheet.Cell(row, col).GetString().Trim();
                    decimal Number(int col) => sheet.Cell(row, col).TryGetValue<decimal>(out var value) ? value : throw new InvalidOperationException($"{Headers[col - 1]} debe ser numérico.");
                    int Integer(int col) { var value = Number(col); if (value != decimal.Truncate(value) || value > int.MaxValue || value < 0) throw new InvalidOperationException($"{Headers[col - 1]} debe ser entero no negativo."); return (int)value; }
                    var category = Text(8); var categoryId = categories.FirstOrDefault(c => c.Name.Equals(category, StringComparison.OrdinalIgnoreCase))?.Id;
                    if (category.Length > 0 && categoryId is null) throw new InvalidOperationException("La categoría no existe. Creala antes de importar.");
                    var command = new CreateProductCommand(current.TenantId!.Value, Text(1), Text(2), Text(3), Number(4), Number(5), Integer(6), Integer(7), categoryId, null, warehouse);
                    var validation = await validator.ValidateAsync(command, ct);
                    if (!validation.IsValid) throw new InvalidOperationException(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
                    if (!existing.Add(command.Sku)) throw new InvalidOperationException("SKU duplicado en el negocio o dentro del archivo.");
                    pending.Add(command);
                }
                catch (InvalidOperationException ex) { errors.Add(new { row, message = ex.Message }); }
            }
            if (errors.Count > 0) return Ok(new { imported = 0, errors, message = "Corregí las filas indicadas. No se importó ningún producto." });
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            foreach (var command in pending) await sender.Send(command, ct);
            await transaction.CommitAsync(ct);
            return Ok(new { imported = pending.Count, errors, message = "Importación completada." });
        }
        catch (InvalidDataException) { return BadRequest(new { message = "El archivo Excel está dañado o no es válido." }); }
    }

    private FileContentResult Download(XLWorkbook book, string name)
    {
        foreach (var sheet in book.Worksheets) { sheet.SheetView.FreezeRows(1); sheet.Row(1).Style.Font.Bold = true; sheet.Columns().AdjustToContents(8, 60); }
        using var output = new MemoryStream(); book.SaveAs(output); return File(output.ToArray(), Mime, name);
    }
}
