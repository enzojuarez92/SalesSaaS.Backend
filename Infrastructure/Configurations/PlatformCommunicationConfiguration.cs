using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class PlatformNotificationConfiguration : IEntityTypeConfiguration<PlatformNotification>
{
    public void Configure(EntityTypeBuilder<PlatformNotification> builder)
    {
        builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.Severity).HasMaxLength(20).IsRequired();
        builder.HasIndex(item => new { item.IsActive, item.TargetTenantId, item.CreatedAtUtc });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TargetTenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PlatformNotificationReadConfiguration : IEntityTypeConfiguration<PlatformNotificationRead>
{
    public void Configure(EntityTypeBuilder<PlatformNotificationRead> builder)
    {
        builder.HasIndex(item => new { item.PlatformNotificationId, item.TenantId }).IsUnique();
        builder.HasOne<PlatformNotification>().WithMany().HasForeignKey(item => item.PlatformNotificationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.Property(item => item.Subject).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Message).HasMaxLength(4000).IsRequired();
        builder.Property(item => item.Response).HasMaxLength(4000);
        builder.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAtUtc });
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupportTicketMessageConfiguration : IEntityTypeConfiguration<SupportTicketMessage>
{
    public void Configure(EntityTypeBuilder<SupportTicketMessage> builder)
    {
        builder.Property(item => item.Message).HasMaxLength(4000).IsRequired();
        builder.HasIndex(item => new { item.SupportTicketId, item.CreatedAtUtc });
        builder.HasOne<SupportTicket>().WithMany().HasForeignKey(item => item.SupportTicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(item => item.SenderUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
