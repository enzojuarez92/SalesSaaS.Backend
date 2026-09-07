using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Authentication.Commands;

public record RegisterTenantCommand(
    string TenantName,
    string TaxId,
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<AuthResponse>;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(command => command.TenantName).NotEmpty().MaximumLength(150);
        RuleFor(command => command.TaxId).NotEmpty().MaximumLength(20);
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.Password).MinimumLength(12)
            .Matches("[A-Z]").WithMessage("La contraseña debe incluir una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe incluir una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe incluir un número.");
    }
}

public sealed class RegisterTenantCommandHandler(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    IRefreshTokenService refreshTokenService) : IRequestHandler<RegisterTenantCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await context.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Ya existe un usuario con ese correo electrónico.");
        }

        var tenant = new Tenant { Id = Guid.NewGuid(), Name = request.TenantName.Trim(), TaxId = request.TaxId.Trim() };
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
        var subscription = new TenantSubscription { Id = Guid.NewGuid(), TenantId = tenant.Id, SubscriptionPlanId = defaultPlan.Id, Status = SubscriptionStatus.Trialing, StartsAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddDays(14), AutoRenew = false };

        context.AddRange(tenant, user, membership, subscription);
        var refreshToken = refreshTokenService.Create(user.Id, tenant.Id);
        context.RefreshTokens.Add(refreshToken.Entity);
        await context.SaveChangesAsync(cancellationToken);

        return AuthResponse.From(tokenService.Create(user, membership), refreshToken, user, membership);
    }
}
