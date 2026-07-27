using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasIndex(c => new { c.TenantId, c.DocumentNumber }).IsUnique();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(150);
        builder.Property(c => c.DocumentType).IsRequired().HasMaxLength(20);
        builder.Property(c => c.DocumentNumber).IsRequired().HasMaxLength(15);
        builder.Property(c => c.TaxCondition).IsRequired().HasMaxLength(50);
        builder.Property(c => c.CreditLimit).HasPrecision(18, 2);
    }
}