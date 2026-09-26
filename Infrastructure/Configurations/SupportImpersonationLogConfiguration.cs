using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class SupportImpersonationLogConfiguration : IEntityTypeConfiguration<SupportImpersonationLog>
{
    public void Configure(EntityTypeBuilder<SupportImpersonationLog> builder)
    {
        builder.Property(item => item.Reason).HasMaxLength(500);
        builder.HasIndex(item => new { item.TenantId, item.StartedAtUtc });
        builder.HasIndex(item => new { item.SuperAdminUserId, item.StartedAtUtc });
        builder.HasIndex(item => item.EndedAtUtc);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.SuperAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.ImpersonatedUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
