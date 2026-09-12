using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class UserWarehouseConfiguration : IEntityTypeConfiguration<UserWarehouse>
{
    public void Configure(EntityTypeBuilder<UserWarehouse> builder)
    {
        builder.HasIndex(item => new { item.UserId, item.TenantId, item.WarehouseId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.WarehouseId });
        builder.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Warehouse).WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.Property(item => item.Provider).IsRequired().HasMaxLength(50);
        builder.Property(item => item.EventType).IsRequired().HasMaxLength(80);
        builder.Property(item => item.ExternalEventId).IsRequired().HasMaxLength(100);
        builder.Property(item => item.PayloadHash).IsRequired().HasMaxLength(64);
        builder.HasIndex(item => new { item.Provider, item.EventType, item.ExternalEventId }).IsUnique();
    }
}
