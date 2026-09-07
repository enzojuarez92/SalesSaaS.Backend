namespace SalesSaaS.Infrastructure.Security;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";
    public int ExpirationDays { get; init; } = 14;
}
