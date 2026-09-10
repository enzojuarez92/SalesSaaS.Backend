using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(order => order.TotalAmount).HasPrecision(18, 2);
        builder.Property(order => order.DiscountAmount).HasPrecision(18, 2);
        builder.Property(order => order.PaymentMethod).HasConversion<int>();
        builder.HasOne(order => order.Customer).WithMany().HasForeignKey(order => order.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Warehouse).WithMany().HasForeignKey(order => order.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(order => order.Tenant).WithMany().HasForeignKey(order => order.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
