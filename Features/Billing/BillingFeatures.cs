using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Billing;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Billing;

public sealed record CreateSubscriptionPlanCommand(string Name, decimal MonthlyPrice, decimal AnnualPrice, string Currency, int MaxUsers, int MaxWarehouses, int MaxInvoicesPerMonth, bool SupportsAfip, bool IsDefault) : IRequest<Guid>;
public sealed record GetSubscriptionPlansQuery() : IRequest<IReadOnlyList<SubscriptionPlanDto>>;
public sealed record SubscribeTenantCommand(Guid TenantId, Guid SubscriptionPlanId, bool AnnualBilling, bool AutoRenew, string PaymentProvider) : IRequest<SubscriptionCheckoutDto>, ITenantScopedRequest;
public sealed record GetTenantSubscriptionQuery(Guid TenantId) : IRequest<TenantSubscriptionDto?>, ITenantScopedRequest;
public sealed record ProcessPaymentWebhookCommand(string Provider, string Payload, string? Signature, string? RequestId) : IRequest;
public sealed record SubscriptionPlanDto(Guid Id, string Name, decimal MonthlyPrice, decimal AnnualPrice, string Currency, int MaxUsers, int MaxWarehouses, int MaxInvoicesPerMonth, bool SupportsAfip, bool IsDefault);
public sealed record TenantSubscriptionDto(Guid Id, Guid SubscriptionPlanId, string PlanName, SubscriptionStatus Status, DateTime StartsAtUtc, DateTime ExpiresAtUtc, bool AutoRenew, string? ProviderSubscriptionId);
public sealed record SubscriptionCheckoutDto(Guid SubscriptionId, Guid SaaSInvoiceId, string CheckoutUrl, string ExternalReference, bool IsSimulated);

public sealed class CreateSubscriptionPlanCommandValidator : AbstractValidator<CreateSubscriptionPlanCommand>
{
    public CreateSubscriptionPlanCommandValidator()
    {
        RuleFor(command => command.Name).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100).WithMessage("El nombre del plan es obligatorio y no puede superar los 100 caracteres.");
        RuleFor(command => command.MonthlyPrice).GreaterThanOrEqualTo(0).WithMessage("El precio mensual no puede ser negativo.");
        RuleFor(command => command.AnnualPrice).GreaterThanOrEqualTo(0).WithMessage("El precio anual no puede ser negativo.");
        RuleFor(command => command.Currency).Length(3).WithMessage("La moneda debe tener tres caracteres.");
        RuleFor(command => command.MaxUsers).GreaterThan(0).WithMessage("El máximo de usuarios debe ser mayor a cero.");
        RuleFor(command => command.MaxWarehouses).GreaterThan(0).WithMessage("El máximo de depósitos debe ser mayor a cero.");
        RuleFor(command => command.MaxInvoicesPerMonth).GreaterThan(0).WithMessage("El máximo mensual de facturas debe ser mayor a cero.");
    }
}
public sealed class SubscribeTenantCommandValidator : AbstractValidator<SubscribeTenantCommand>
{
    public SubscribeTenantCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty().WithMessage("El negocio es obligatorio.");
        RuleFor(command => command.SubscriptionPlanId).NotEmpty().WithMessage("El plan es obligatorio.");
        RuleFor(command => command.PaymentProvider).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(50).WithMessage("La pasarela de pago es obligatoria.");
    }
}

public sealed class CreateSubscriptionPlanCommandHandler(ApplicationDbContext context) : IRequestHandler<CreateSubscriptionPlanCommand, Guid>
{
    public async Task<Guid> Handle(CreateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await context.SubscriptionPlans.AnyAsync(plan => plan.Name == name, cancellationToken)) throw new InvalidOperationException("Ya existe un plan con ese nombre.");
        if (request.IsDefault) await context.SubscriptionPlans.Where(plan => plan.IsDefault).ExecuteUpdateAsync(setters => setters.SetProperty(plan => plan.IsDefault, false), cancellationToken);
        var plan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = name, MonthlyPrice = request.MonthlyPrice, AnnualPrice = request.AnnualPrice, Currency = request.Currency.Trim().ToUpperInvariant(), MaxUsers = request.MaxUsers, MaxWarehouses = request.MaxWarehouses, MaxInvoicesPerMonth = request.MaxInvoicesPerMonth, SupportsAfip = request.SupportsAfip, IsDefault = request.IsDefault };
        context.SubscriptionPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);
        return plan.Id;
    }
}
public sealed class GetSubscriptionPlansQueryHandler(ApplicationDbContext context) : IRequestHandler<GetSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    public async Task<IReadOnlyList<SubscriptionPlanDto>> Handle(GetSubscriptionPlansQuery request, CancellationToken cancellationToken) =>
        await context.SubscriptionPlans.AsNoTracking().Where(plan => plan.IsActive).OrderBy(plan => plan.MonthlyPrice).Select(plan => new SubscriptionPlanDto(plan.Id, plan.Name, plan.MonthlyPrice, plan.AnnualPrice, plan.Currency, plan.MaxUsers, plan.MaxWarehouses, plan.MaxInvoicesPerMonth, plan.SupportsAfip, plan.IsDefault)).ToListAsync(cancellationToken);
}
public sealed class SubscribeTenantCommandHandler(ApplicationDbContext context, IPaymentGatewayService paymentGatewayService) : IRequestHandler<SubscribeTenantCommand, SubscriptionCheckoutDto>
{
    public async Task<SubscriptionCheckoutDto> Handle(SubscribeTenantCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans.SingleOrDefaultAsync(plan => plan.Id == request.SubscriptionPlanId && plan.IsActive, cancellationToken) ?? throw new InvalidOperationException("El plan seleccionado no existe o no está activo.");
        var now = DateTime.UtcNow;
        var subscription = new TenantSubscription { Id = Guid.NewGuid(), TenantId = request.TenantId, SubscriptionPlanId = plan.Id, Status = SubscriptionStatus.PastDue, StartsAtUtc = now, ExpiresAtUtc = request.AnnualBilling ? now.AddYears(1) : now.AddMonths(1), AutoRenew = request.AutoRenew };
        var invoice = new SaaSInvoice { Id = Guid.NewGuid(), TenantId = request.TenantId, TenantSubscriptionId = subscription.Id, Amount = request.AnnualBilling ? plan.AnnualPrice : plan.MonthlyPrice, Currency = plan.Currency, PaymentProvider = request.PaymentProvider.Trim(), DueAtUtc = now.AddDays(7), ExternalReference = $"pending-{Guid.NewGuid():N}" };
        var checkout = await paymentGatewayService.CreateSubscriptionCheckoutAsync(new PaymentCheckoutRequest(invoice.Id, request.TenantId, invoice.Amount, invoice.Currency, $"Suscripción {plan.Name}", invoice.PaymentProvider), cancellationToken);
        invoice.ExternalReference = checkout.ExternalReference;
        invoice.CheckoutUrl = checkout.CheckoutUrl;
        subscription.ProviderSubscriptionId = checkout.ProviderSubscriptionId;
        if (checkout.IsSimulated)
        {
            subscription.Status = SubscriptionStatus.Active;
            invoice.Status = SaaSInvoiceStatus.Paid;
            invoice.PaidAtUtc = now;
        }
        context.AddRange(subscription, invoice);
        await context.SaveChangesAsync(cancellationToken);
        return new SubscriptionCheckoutDto(subscription.Id, invoice.Id, checkout.CheckoutUrl, checkout.ExternalReference, checkout.IsSimulated);
    }
}
public sealed class GetTenantSubscriptionQueryHandler(ApplicationDbContext context) : IRequestHandler<GetTenantSubscriptionQuery, TenantSubscriptionDto?>
{
    public async Task<TenantSubscriptionDto?> Handle(GetTenantSubscriptionQuery request, CancellationToken cancellationToken) =>
        await context.TenantSubscriptions.AsNoTracking().Where(subscription => subscription.TenantId == request.TenantId).OrderByDescending(subscription => (subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.Trialing) && subscription.ExpiresAtUtc > DateTime.UtcNow).ThenByDescending(subscription => subscription.StartsAtUtc)
            .Select(subscription => new TenantSubscriptionDto(subscription.Id, subscription.SubscriptionPlanId, subscription.SubscriptionPlan!.Name, subscription.Status, subscription.StartsAtUtc, subscription.ExpiresAtUtc, subscription.AutoRenew, subscription.ProviderSubscriptionId)).FirstOrDefaultAsync(cancellationToken);
}
public sealed class ProcessPaymentWebhookCommandHandler(ApplicationDbContext context, IPaymentGatewayService paymentGatewayService, ILogger<ProcessPaymentWebhookCommandHandler> logger) : IRequestHandler<ProcessPaymentWebhookCommand>
{
    public async Task Handle(ProcessPaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        var result = await paymentGatewayService.ProcessWebhookAsync(request.Provider, request.Payload, new PaymentWebhookHeaders(request.Signature, request.RequestId), cancellationToken);
        if (!result.IsValid || string.IsNullOrWhiteSpace(result.ExternalReference)) throw new InvalidOperationException(result.Error ?? "El webhook de pago no es válido.");
        var eventType = result.EventType ?? "payment.updated";
        var externalEventId = result.ExternalEventId ?? result.ProviderSubscriptionId ?? throw new InvalidOperationException("El webhook no identifica el evento de pago.");
        if (await context.PaymentWebhookEvents.AnyAsync(item => item.Provider == request.Provider && item.EventType == eventType && item.ExternalEventId == externalEventId, cancellationToken)) return;
        var invoice = await context.SaaSInvoices.SingleOrDefaultAsync(item => item.ExternalReference == result.ExternalReference, cancellationToken) ?? throw new InvalidOperationException("No existe un cobro SaaS para la referencia recibida.");
        context.PaymentWebhookEvents.Add(new PaymentWebhookEvent { Id = Guid.NewGuid(), Provider = request.Provider, EventType = eventType, ExternalEventId = externalEventId, PayloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Payload))).ToLowerInvariant() });
        if (invoice.Status == SaaSInvoiceStatus.Paid) { await context.SaveChangesAsync(cancellationToken); return; }
        if (result.IsPaid && !result.IsSimulated && (result.Amount != invoice.Amount || !string.Equals(result.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("El importe o la moneda del pago no coincide con la suscripción.");
        if (!result.IsPaid) { invoice.Status = SaaSInvoiceStatus.Failed; await context.SaveChangesAsync(cancellationToken); return; }
        invoice.Status = SaaSInvoiceStatus.Paid;
        invoice.PaidAtUtc = DateTime.UtcNow;
        var subscription = await context.TenantSubscriptions.SingleAsync(item => item.Id == invoice.TenantSubscriptionId, cancellationToken);
        subscription.Status = SubscriptionStatus.Active;
        subscription.ProviderSubscriptionId ??= result.ProviderSubscriptionId;
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("SaaS payment {SaaSInvoiceId} completed for tenant {TenantId}", invoice.Id, invoice.TenantId);
    }
}
