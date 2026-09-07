using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Notifications;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Notifications;

public sealed record GetMyNotificationsQuery(Guid TenantId, Guid UserId, bool UnreadOnly = false) : IRequest<IReadOnlyList<NotificationDto>>, ITenantScopedRequest;
public sealed record MarkNotificationReadCommand(Guid TenantId, Guid UserId, Guid NotificationId) : IRequest, ITenantScopedRequest;
public sealed record SendAccountReceivableRemindersCommand(Guid TenantId) : IRequest<int>, ITenantScopedRequest;
public sealed record SendSubscriptionExpirationAlertsCommand(Guid TenantId, int DaysBeforeExpiration = 7) : IRequest<int>, ITenantScopedRequest;
public sealed record NotificationDto(Guid Id, string Title, string Message, bool IsRead, DateTime CreatedAtUtc, DateTime? ReadAtUtc);
public sealed record WelcomeTenantRegisteredEvent(Guid TenantId, Guid UserId, string UserName, string Email, string TenantName) : INotification;
public sealed record LowStockReachedEvent(Guid TenantId, Guid ProductId, string ProductName, int CurrentStock) : INotification;
public sealed record InvoiceAuthorizedEvent(Guid TenantId, Guid InvoiceId, string InvoiceNumber, string Cae, string? CustomerEmail, string CustomerName, decimal TotalAmount) : INotification;

public sealed class GetMyNotificationsQueryHandler(ApplicationDbContext context) : IRequestHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationDto>>
{
    public async Task<IReadOnlyList<NotificationDto>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken) =>
        await context.Notifications.AsNoTracking().Where(notification => notification.TenantId == request.TenantId && notification.UserId == request.UserId && (!request.UnreadOnly || !notification.IsRead))
            .OrderByDescending(notification => notification.CreatedAtUtc).Select(notification => new NotificationDto(notification.Id, notification.Title, notification.Message, notification.IsRead, notification.CreatedAtUtc, notification.ReadAtUtc)).ToListAsync(cancellationToken);
}
public sealed class MarkNotificationReadCommandHandler(ApplicationDbContext context) : IRequestHandler<MarkNotificationReadCommand>
{
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await context.Notifications.SingleOrDefaultAsync(item => item.Id == request.NotificationId && item.TenantId == request.TenantId && item.UserId == request.UserId, cancellationToken) ?? throw new InvalidOperationException("La notificación no existe.");
        if (!notification.IsRead) { notification.IsRead = true; notification.ReadAtUtc = DateTime.UtcNow; await context.SaveChangesAsync(cancellationToken); }
    }
}
public sealed class SendAccountReceivableRemindersCommandHandler(ApplicationDbContext context, IPublisher publisher) : IRequestHandler<SendAccountReceivableRemindersCommand, int>
{
    public async Task<int> Handle(SendAccountReceivableRemindersCommand request, CancellationToken cancellationToken)
    {
        var balances = await context.CustomerAccountEntries.AsNoTracking().Where(entry => entry.TenantId == request.TenantId)
            .GroupBy(entry => new { entry.CustomerId, entry.Customer!.Name, entry.Customer.Email }).Select(group => new { group.Key.CustomerId, group.Key.Name, group.Key.Email, Balance = group.Sum(entry => entry.Type == CustomerAccountEntryType.Debit ? entry.Amount : -entry.Amount) }).Where(item => item.Balance > 0 && item.Email != string.Empty).ToListAsync(cancellationToken);
        foreach (var item in balances) await publisher.Publish(new AccountReceivableReminderEvent(request.TenantId, item.CustomerId, item.Name, item.Email, item.Balance), cancellationToken);
        return balances.Count;
    }
}
public sealed class SendSubscriptionExpirationAlertsCommandHandler(ApplicationDbContext context, IPublisher publisher) : IRequestHandler<SendSubscriptionExpirationAlertsCommand, int>
{
    public async Task<int> Handle(SendSubscriptionExpirationAlertsCommand request, CancellationToken cancellationToken)
    {
        var subscription = await context.TenantSubscriptions.AsNoTracking().Where(item => item.TenantId == request.TenantId && (item.Status == SubscriptionStatus.Active || item.Status == SubscriptionStatus.Trialing)).OrderByDescending(item => item.ExpiresAtUtc).Select(item => new { item.ExpiresAtUtc, PlanName = item.SubscriptionPlan!.Name }).FirstOrDefaultAsync(cancellationToken);
        if (subscription is null || subscription.ExpiresAtUtc > DateTime.UtcNow.AddDays(request.DaysBeforeExpiration)) return 0;
        await publisher.Publish(new SubscriptionExpiringEvent(request.TenantId, subscription.PlanName, subscription.ExpiresAtUtc), cancellationToken);
        return 1;
    }
}

public sealed record AccountReceivableReminderEvent(Guid TenantId, Guid CustomerId, string CustomerName, string Email, decimal Balance) : INotification;
public sealed record SubscriptionExpiringEvent(Guid TenantId, string PlanName, DateTime ExpiresAtUtc) : INotification;

public sealed class WelcomeTenantRegisteredEventHandler(ApplicationDbContext context, IEmailTemplateService templates, IEmailQueue emailQueue) : INotificationHandler<WelcomeTenantRegisteredEvent>
{
    public async Task Handle(WelcomeTenantRegisteredEvent notification, CancellationToken cancellationToken)
    {
        context.Notifications.Add(new Notification { Id = Guid.NewGuid(), TenantId = notification.TenantId, UserId = notification.UserId, Title = "Bienvenido a SalesSaaS", Message = $"Tu negocio {notification.TenantName} fue creado correctamente." });
        await context.SaveChangesAsync(cancellationToken);
        await emailQueue.QueueAsync(new EmailMessage(notification.Email, "Bienvenido a SalesSaaS", templates.Render("Welcome", new Dictionary<string, string> { ["Name"] = notification.UserName, ["TenantName"] = notification.TenantName })), cancellationToken);
    }
}
public sealed class LowStockReachedEventHandler(ApplicationDbContext context, IEmailTemplateService templates, IEmailQueue emailQueue) : INotificationHandler<LowStockReachedEvent>
{
    public async Task Handle(LowStockReachedEvent notification, CancellationToken cancellationToken)
    {
        var administrators = await context.TenantMemberships.Include(member => member.User).Where(member => member.TenantId == notification.TenantId && member.IsActive && (member.Role == Roles.Owner || member.Role == Roles.Admin)).Select(member => new { member.UserId, member.User!.Email }).ToListAsync(cancellationToken);
        const string title = "Alerta de stock bajo";
        var message = $"El producto {notification.ProductName} alcanzó el stock mínimo. Stock actual: {notification.CurrentStock}.";
        context.Notifications.AddRange(administrators.Select(administrator => new Notification { Id = Guid.NewGuid(), TenantId = notification.TenantId, UserId = administrator.UserId, Title = title, Message = message }));
        await context.SaveChangesAsync(cancellationToken);
        var body = templates.Render("LowStock", new Dictionary<string, string> { ["ProductName"] = notification.ProductName, ["CurrentStock"] = notification.CurrentStock.ToString() });
        foreach (var administrator in administrators) await emailQueue.QueueAsync(new EmailMessage(administrator.Email, title, body), cancellationToken);
    }
}
public sealed class InvoiceAuthorizedEventHandler(IEmailTemplateService templates, IEmailQueue emailQueue) : INotificationHandler<InvoiceAuthorizedEvent>
{
    public async Task Handle(InvoiceAuthorizedEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.CustomerEmail)) return;
        var detail = $"Comprobante: {notification.InvoiceNumber}\nCAE: {notification.Cae}\nTotal: {notification.TotalAmount:N2}";
        var attachment = new EmailAttachment($"comprobante-{notification.InvoiceNumber}.txt", "text/plain", System.Text.Encoding.UTF8.GetBytes(detail));
        var body = templates.Render("Invoice", new Dictionary<string, string> { ["InvoiceNumber"] = notification.InvoiceNumber, ["Cae"] = notification.Cae });
        await emailQueue.QueueAsync(new EmailMessage(notification.CustomerEmail, "Tu comprobante electrónico", body, [attachment]), cancellationToken);
    }
}
public sealed class AccountReceivableReminderEventHandler(IEmailTemplateService templates, IEmailQueue emailQueue) : INotificationHandler<AccountReceivableReminderEvent>
{
    public async Task Handle(AccountReceivableReminderEvent notification, CancellationToken cancellationToken) =>
        await emailQueue.QueueAsync(new EmailMessage(notification.Email, "Recordatorio de saldo pendiente", templates.Render("AccountReminder", new Dictionary<string, string> { ["CustomerName"] = notification.CustomerName, ["Balance"] = notification.Balance.ToString("N2") })), cancellationToken);
}
public sealed class SubscriptionExpiringEventHandler(ApplicationDbContext context, IEmailTemplateService templates, IEmailQueue emailQueue) : INotificationHandler<SubscriptionExpiringEvent>
{
    public async Task Handle(SubscriptionExpiringEvent notification, CancellationToken cancellationToken)
    {
        var administrators = await context.TenantMemberships.Include(member => member.User).Where(member => member.TenantId == notification.TenantId && member.IsActive && (member.Role == Roles.Owner || member.Role == Roles.Admin)).Select(member => new { member.UserId, member.User!.Email }).ToListAsync(cancellationToken);
        const string title = "Tu suscripción está por vencer";
        var message = $"El plan {notification.PlanName} vence el {notification.ExpiresAtUtc:dd/MM/yyyy}.";
        context.Notifications.AddRange(administrators.Select(administrator => new Notification { Id = Guid.NewGuid(), TenantId = notification.TenantId, UserId = administrator.UserId, Title = title, Message = message }));
        await context.SaveChangesAsync(cancellationToken);
        var body = templates.Render("SubscriptionExpiring", new Dictionary<string, string> { ["PlanName"] = notification.PlanName, ["ExpirationDate"] = notification.ExpiresAtUtc.ToString("dd/MM/yyyy") });
        foreach (var administrator in administrators) await emailQueue.QueueAsync(new EmailMessage(administrator.Email, title, body), cancellationToken);
    }
}
