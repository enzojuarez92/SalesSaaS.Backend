using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(notification => notification.Title).HasMaxLength(200);
        builder.Property(notification => notification.Message).HasMaxLength(2000);
        builder.HasIndex(notification => new { notification.TenantId, notification.UserId, notification.IsRead, notification.CreatedAtUtc });
        builder.HasOne<User>().WithMany().HasForeignKey(notification => notification.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(notification => notification.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
