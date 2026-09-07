using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SalesSaaS.Application.Notifications;

namespace SalesSaaS.Infrastructure.Notifications;

public sealed class MailKitEmailService(IOptions<EmailOptions> options, ILogger<MailKitEmailService> logger) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (string.IsNullOrWhiteSpace(configuration.SmtpHost) || string.IsNullOrWhiteSpace(configuration.SenderEmail))
        {
            logger.LogWarning("Email was queued for {Recipient} but SMTP is not configured", message.To);
            return;
        }
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(configuration.SenderName, configuration.SenderEmail));
        email.To.Add(MailboxAddress.Parse(message.To));
        email.Subject = message.Subject;
        var body = new BodyBuilder { HtmlBody = message.HtmlBody };
        foreach (var attachment in message.Attachments ?? []) body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        email.Body = body.ToMessageBody();
        using var client = new SmtpClient();
        await client.ConnectAsync(configuration.SmtpHost, configuration.SmtpPort, configuration.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        if (!string.IsNullOrWhiteSpace(configuration.UserName)) await client.AuthenticateAsync(configuration.UserName, configuration.Password, cancellationToken);
        await client.SendAsync(email, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
