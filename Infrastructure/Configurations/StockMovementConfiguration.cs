using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasIndex(movement => new { movement.TenantId, movement.ProductId, movement.WarehouseId, movement.OccurredAtUtc });
        builder.Property(movement => movement.Reason).HasMaxLength(300);
        builder.Property(movement => movement.Reference).HasMaxLength(100);
        builder.HasOne(movement => movement.Product).WithMany().HasForeignKey(movement => movement.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.Warehouse).WithMany().HasForeignKey(movement => movement.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}
