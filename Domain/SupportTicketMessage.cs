namespace SalesSaaS.Domain;

public sealed class SupportTicketMessage
{
    public Guid Id { get; set; }
    public Guid SupportTicketId { get; set; }
    public Guid? SenderUserId { get; set; }
    public bool IsFromSupport { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
