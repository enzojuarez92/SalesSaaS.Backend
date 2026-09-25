using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Configurations;

public sealed class ApiEndpointMetricConfiguration : IEntityTypeConfiguration<ApiEndpointMetric>
{
    public void Configure(EntityTypeBuilder<ApiEndpointMetric> builder)
    {
        builder.Property(item => item.Method).HasMaxLength(10);
        builder.Property(item => item.Path).HasMaxLength(300);
        builder.HasIndex(item => new { item.PeriodStartUtc, item.Method, item.Path }).IsUnique();
        builder.HasIndex(item => item.LastOccurredAtUtc);
    }
}

public sealed class PlatformErrorLogConfiguration : IEntityTypeConfiguration<PlatformErrorLog>
{
    public void Configure(EntityTypeBuilder<PlatformErrorLog> builder)
    {
        builder.Property(item => item.Method).HasMaxLength(10);
        builder.Property(item => item.Path).HasMaxLength(300);
        builder.Property(item => item.UserEmail).HasMaxLength(320);
        builder.Property(item => item.ErrorType).HasMaxLength(200);
        builder.Property(item => item.Message).HasMaxLength(2000);
        builder.Property(item => item.TraceId).HasMaxLength(100);
        builder.HasIndex(item => item.OccurredAtUtc);
        builder.HasIndex(item => item.TenantId);
    }
}
