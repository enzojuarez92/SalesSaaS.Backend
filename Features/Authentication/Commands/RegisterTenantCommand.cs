using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;
using SalesSaaS.Features.Notifications;
using SalesSaaS.Application.Validation;

namespace SalesSaaS.Features.Authentication.Commands;

public record RegisterTenantCommand(
    string TenantName,
    string TaxId,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string? TaxCondition = null,
    string? BusinessCategory = null) : IRequest<AuthResponse>;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(command => command.TenantName).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(150).WithMessage("El nombre del negocio es obligatorio y no puede superar los 150 caracteres.");
        RuleFor(command => command.TaxId).Must(value => string.IsNullOrWhiteSpace(value) || ArgentineTaxId.IsValid(value)).WithMessage("El CUIT debe contener exactamente 11 dígitos numéricos y ser válido.");
        RuleFor(command => command.FirstName).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100).WithMessage("El nombre es obligatorio y no puede superar los 100 caracteres.");
        RuleFor(command => command.LastName).Must(value => !string.IsNullOrWhiteSpace(value)).MaximumLength(100).WithMessage("El apellido es obligatorio y no puede superar los 100 caracteres.");
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256).WithMessage("Ingresá un correo electrónico válido.");
        RuleFor(command => command.Password).MinimumLength(12)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir un número.");
        RuleFor(command => command.TaxCondition).MaximumLength(80).When(command => command.TaxCondition is not null);
        RuleFor(command => command.BusinessCategory).MaximumLength(100).When(command => command.BusinessCategory is not null);
    }
}

public sealed class RegisterTenantCommandHandler(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    IRefreshTokenService refreshTokenService,
    IPublisher publisher) : IRequestHandler<RegisterTenantCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await context.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Ya existe un usuario con ese correo electrónico.");
        }

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = request.TenantName.Trim(), TaxId = request.TaxId.Trim(), TaxCondition = request.TaxCondition?.Trim(), BusinessCategory = request.BusinessCategory?.Trim() };
        var user = new User { Id = Guid.NewGuid(), FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Email = email };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var membership = new TenantMembership
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = Roles.Owner
        };

        var defaultPlan = await context.SubscriptionPlans.SingleOrDefaultAsync(plan => plan.IsDefault && plan.IsActive, cancellationToken);
        if (defaultPlan is null)
        {
            defaultPlan = new SubscriptionPlan { Id = Guid.NewGuid(), Name = "Basic", MonthlyPrice = 0, AnnualPrice = 0, Currency = "ARS", MaxUsers = 3, MaxWarehouses = 1, MaxInvoicesPerMonth = 25, SupportsAfip = false, IsDefault = true };
            context.SubscriptionPlans.Add(defaultPlan);
        }
        var subscription = new TenantSubscription { Id = Guid.NewGuid(), TenantId = tenant.Id, SubscriptionPlanId = defaultPlan.Id, Status = SubscriptionStatus.Trialing, StartsAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddDays(7), AutoRenew = false };
        var consumerFinal = new Customer
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Consumidor Final", LegalName = "Consumidor Final",
            DocumentType = "DNI", DocumentNumber = "00000000", TaxCondition = "Consumidor Final",
            IsActive = true, AllowCredit = false
        };

        var mainWarehouse = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Code = "MAIN", Name = "Depósito Principal", IsActive = true
        };
        context.AddRange(tenant, user, membership, subscription, consumerFinal, mainWarehouse);
        var refreshToken = refreshTokenService.Create(user.Id, tenant.Id);
        context.RefreshTokens.Add(refreshToken.Entity);
        await context.SaveChangesAsync(cancellationToken);
        await publisher.Publish(new WelcomeTenantRegisteredEvent(tenant.Id, user.Id, $"{user.FirstName} {user.LastName}", user.Email, tenant.Name), cancellationToken);

        return AuthResponse.From(tokenService.Create(user, membership), refreshToken, user, membership);
    }
}
