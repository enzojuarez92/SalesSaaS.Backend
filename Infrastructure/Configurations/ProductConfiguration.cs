using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // El índice único que tenías en el DbContext pasa acá
        builder.HasIndex(p => new { p.TenantId, p.Sku }).IsUnique();

        // Opcional: Podés ir agregándole límites si querés
        builder.Property(p => p.Sku).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.Cost).HasPrecision(18, 2);
        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}
