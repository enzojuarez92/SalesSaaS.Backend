using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure;

public static class DefaultCategorySeeder
{
    private static readonly (string Name, string Description)[] Definitions =
    [
        ("Almacén", "Productos de despensa y consumo diario."),
        ("Bebidas sin alcohol", "Aguas, gaseosas, jugos y energizantes."),
        ("Bebidas alcohólicas", "Cervezas, vinos y aperitivos."),
        ("Lácteos y huevos", "Leche, quesos, yogures y huevos."),
        ("Carnes y fiambres", "Carnes frescas, embutidos y fiambres."),
        ("Frutas y verduras", "Productos frescos de verdulería."),
        ("Panadería", "Panificados, facturas y repostería."),
        ("Congelados", "Alimentos congelados y helados."),
        ("Kiosco y golosinas", "Golosinas, cigarrillos y artículos de kiosco."),
        ("Snacks", "Papas, maníes, galletitas y bocadillos."),
        ("Limpieza", "Limpieza del hogar y lavandería."),
        ("Perfumería y cuidado personal", "Higiene y cuidado personal."),
        ("Bebés", "Pañales, toallitas y cuidado infantil."),
        ("Mascotas", "Alimentos y accesorios para mascotas."),
        ("Hogar y bazar", "Artículos para el hogar y cocina."),
        ("Electrónica y accesorios", "Accesorios electrónicos y tecnología."),
        ("Librería", "Papelería y útiles escolares."),
        ("Ferretería", "Herramientas e insumos de reparación."),
        ("Indumentaria", "Ropa y accesorios personales."),
        ("Otros", "Productos sin una categoría específica.")
    ];

    public static IEnumerable<Category> CreateDefaults(Guid tenantId) => Definitions.Select(item => new Category
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Name = item.Name, Description = item.Description, IsActive = true
    });

    public static async Task EnsureForEveryTenantAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters().Select(tenant => tenant.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
        {
            var existing = await context.Categories.IgnoreQueryFilters().Where(category => category.TenantId == tenantId).Select(category => category.Name).ToListAsync(cancellationToken);
            var names = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
            context.Categories.AddRange(CreateDefaults(tenantId).Where(category => !names.Contains(category.Name)));
        }
        if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
    }
}
