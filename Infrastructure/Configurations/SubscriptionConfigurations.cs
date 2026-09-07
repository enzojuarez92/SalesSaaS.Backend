using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.HasIndex(plan => plan.Name).IsUnique();
        builder.Property(plan => plan.Name).HasMaxLength(100);
        builder.Property(plan => plan.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(plan => plan.AnnualPrice).HasPrecision(18, 2);
        builder.Property(plan => plan.Currency).HasMaxLength(3);
    }
}

public sealed class TenantSubscriptionConfiguration : IEntityTypeConfiguration<TenantSubscription>
{
    public void Configure(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.HasIndex(subscription => new { subscription.TenantId, subscription.Status });
        builder.Property(subscription => subscription.ProviderSubscriptionId).HasMaxLength(200);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(subscription => subscription.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(subscription => subscription.SubscriptionPlan).WithMany().HasForeignKey(subscription => subscription.SubscriptionPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SaaSInvoiceConfiguration : IEntityTypeConfiguration<SaaSInvoice>
{
    public void Configure(EntityTypeBuilder<SaaSInvoice> builder)
    {
        builder.HasIndex(invoice => invoice.ExternalReference).IsUnique();
        builder.Property(invoice => invoice.Amount).HasPrecision(18, 2);
        builder.Property(invoice => invoice.Currency).HasMaxLength(3);
        builder.Property(invoice => invoice.PaymentProvider).HasMaxLength(50);
        builder.Property(invoice => invoice.ExternalReference).HasMaxLength(200);
        builder.Property(invoice => invoice.CheckoutUrl).HasMaxLength(1000);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(invoice => invoice.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TenantSubscription>().WithMany().HasForeignKey(invoice => invoice.TenantSubscriptionId).OnDelete(DeleteBehavior.Restrict);
    }
}
