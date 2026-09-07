namespace SalesSaaS.Infrastructure.Notifications;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string SenderEmail { get; init; } = string.Empty;
    public string SenderName { get; init; } = "SalesSaaS";
}
