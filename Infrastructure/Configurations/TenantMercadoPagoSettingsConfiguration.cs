using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class TenantMercadoPagoSettingsConfiguration : IEntityTypeConfiguration<TenantMercadoPagoSettings>
{
    public void Configure(EntityTypeBuilder<TenantMercadoPagoSettings> builder)
    {
        builder.HasIndex(settings => settings.TenantId).IsUnique();
        builder.Property(settings => settings.PublicKey).HasMaxLength(300);
        builder.Property(settings => settings.AccessTokenEncrypted).HasMaxLength(4000);
        builder.Property(settings => settings.WebhookSecretEncrypted).HasMaxLength(4000);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(settings => settings.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}
