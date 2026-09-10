using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(log => log.EntityName).HasMaxLength(200);
        builder.Property(log => log.ChangesJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(log => new { log.TenantId, log.TimestampUtc });
        builder.HasIndex(log => new { log.TenantId, log.EntityName, log.TimestampUtc });
        builder.HasIndex(log => new { log.TenantId, log.WarehouseId, log.TimestampUtc });
        builder.HasOne<User>().WithMany().HasForeignKey(log => log.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(log => log.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Warehouse>().WithMany().HasForeignKey(log => log.WarehouseId).OnDelete(DeleteBehavior.Restrict);
    }
}
