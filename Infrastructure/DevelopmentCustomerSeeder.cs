using Microsoft.EntityFrameworkCore;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure;

public static class DevelopmentCustomerSeeder
{
    private static readonly (string Name, string Document, string Email, string Phone, bool AllowCredit, decimal CreditLimit)[] Definitions =
    [
        ("Ana Martínez", "28123456", "ana.martinez@example.test", "3815550101", false, 0m),
        ("Bruno Rodríguez", "30234567", "bruno.rodriguez@example.test", "3815550102", true, 50000m),
        ("Carla Gómez", "31567890", "carla.gomez@example.test", "3815550103", false, 0m),
        ("Diego Fernández", "32890123", "diego.fernandez@example.test", "3815550104", true, 75000m),
        ("Elena López", "34123456", "elena.lopez@example.test", "3815550105", false, 0m),
        ("Franco Díaz", "35678901", "franco.diaz@example.test", "3815550106", true, 100000m),
        ("Gabriela Torres", "36901234", "gabriela.torres@example.test", "3815550107", false, 0m),
        ("Hugo Romero", "37234567", "hugo.romero@example.test", "3815550108", true, 35000m)
    ];

    public static IEnumerable<Customer> CreateDefaults(Guid tenantId) => Definitions.Select(item => new Customer
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Name = item.Name, LegalName = item.Name,
        DocumentType = "DNI", DocumentNumber = item.Document, TaxCondition = "Consumidor Final",
        Email = item.Email, Phone = item.Phone, Address = "Dirección de prueba", City = "San Miguel de Tucumán",
        State = "Tucumán", PostalCode = "4000", AllowCredit = item.AllowCredit, CreditLimit = item.CreditLimit, IsActive = true
    });

    public static async Task EnsureForEveryTenantAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var tenantIds = await context.Tenants.IgnoreQueryFilters().Select(tenant => tenant.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
        {
            var documents = await context.Customers.IgnoreQueryFilters().Where(customer => customer.TenantId == tenantId).Select(customer => customer.DocumentNumber).ToListAsync(cancellationToken);
            var existing = documents.ToHashSet(StringComparer.OrdinalIgnoreCase);
            context.Customers.AddRange(CreateDefaults(tenantId).Where(customer => !existing.Contains(customer.DocumentNumber)));
        }
        if (context.ChangeTracker.HasChanges()) await context.SaveChangesAsync(cancellationToken);
    }
}
