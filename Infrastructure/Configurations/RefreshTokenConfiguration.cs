using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.Property(token => token.TokenHash).IsRequired().HasMaxLength(64);

        builder.HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(token => token.Tenant)
            .WithMany()
            .HasForeignKey(token => token.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
