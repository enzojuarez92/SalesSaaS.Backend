using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.Property(item => item.Status).HasMaxLength(30);
        builder.Property(item => item.TotalAmount).HasPrecision(18, 2);
        builder.HasOne(item => item.Customer).WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    public void Configure(EntityTypeBuilder<QuoteItem> builder)
    {
        builder.Property(item => item.UnitPrice).HasPrecision(18, 2); builder.Property(item => item.TotalAmount).HasPrecision(18, 2);
        builder.HasOne(item => item.Quote).WithMany(quote => quote.Items).HasForeignKey(item => item.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Product).WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasIndex(item => new { item.TenantId, item.Number }).IsUnique(); builder.Property(item => item.Number).HasMaxLength(50); builder.Property(item => item.Status).HasMaxLength(30); builder.Property(item => item.TotalAmount).HasPrecision(18, 2); builder.Property(item => item.Cae).HasMaxLength(20); builder.Property(item => item.AfipResult).HasMaxLength(20); builder.Property(item => item.BarCode).HasMaxLength(1000); builder.Property(item => item.AfipErrors).HasMaxLength(4000);
        builder.HasOne(item => item.Order).WithMany().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Customer).WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class TenantFiscalProfileConfiguration : IEntityTypeConfiguration<TenantFiscalProfile>
{
    public void Configure(EntityTypeBuilder<TenantFiscalProfile> builder)
    {
        builder.HasIndex(item => item.TenantId).IsUnique();
        builder.Property(item => item.IssuerTaxId).HasMaxLength(20);
        builder.Property(item => item.CertificateAlias).HasMaxLength(100);
        builder.Property(item => item.CertificateContentEncrypted).HasMaxLength(16000);
        builder.Property(item => item.PrivateKeyContentEncrypted).HasMaxLength(16000);
        builder.Property(item => item.CertificatePassphraseEncrypted).HasMaxLength(16000);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CustomerAccountEntryConfiguration : IEntityTypeConfiguration<CustomerAccountEntry>
{
    public void Configure(EntityTypeBuilder<CustomerAccountEntry> builder)
    {
        builder.Property(item => item.Amount).HasPrecision(18, 2); builder.Property(item => item.Description).HasMaxLength(300);
        builder.HasOne(item => item.Customer).WithMany().HasForeignKey(item => item.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Invoice).WithMany().HasForeignKey(item => item.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Warehouse).WithMany().HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TenantId, item.WarehouseId, item.CustomerId, item.OccurredAtUtc });
    }
}
