using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.HasIndex(warehouse => new { warehouse.TenantId, warehouse.Code }).IsUnique();
        builder.Property(warehouse => warehouse.Code).IsRequired().HasMaxLength(30);
        builder.Property(warehouse => warehouse.Name).IsRequired().HasMaxLength(100);
        builder.Property(warehouse => warehouse.Address).HasMaxLength(250);
    }
}
