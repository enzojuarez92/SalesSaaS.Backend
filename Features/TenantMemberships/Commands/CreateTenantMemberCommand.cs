using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Application.Billing;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.TenantMemberships.Commands;

public record CreateTenantMemberCommand(
    Guid TenantId,
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string Role) : IRequest<Guid>, ITenantScopedRequest;

public sealed class CreateTenantMemberCommandValidator : AbstractValidator<CreateTenantMemberCommand>
{
    private static readonly string[] AssignableRoles = [Roles.Admin, Roles.Seller, Roles.Warehouse];

    public CreateTenantMemberCommandValidator()
    {
        RuleFor(command => command.TenantId).NotEmpty();
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.Password).MinimumLength(12);
        RuleFor(command => command.Role).Must(AssignableRoles.Contains)
            .WithMessage("El rol debe ser Admin, Seller o Warehouse.");
    }
}

public sealed class CreateTenantMemberCommandHandler(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    ISubscriptionGatekeeper subscriptionGatekeeper) : IRequestHandler<CreateTenantMemberCommand, Guid>
{
    public async Task<Guid> Handle(CreateTenantMemberCommand request, CancellationToken cancellationToken)
    {
        await subscriptionGatekeeper.EnsureCanAddUserAsync(request.TenantId, cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        if (await context.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            throw new InvalidOperationException("Ya existe un usuario con ese correo electrónico.");
        }

        var user = new User { Id = Guid.NewGuid(), FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), Email = email };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var membership = new TenantMembership
        {
            Id = Guid.NewGuid(), UserId = user.Id, TenantId = request.TenantId, Role = request.Role
        };

        context.AddRange(user, membership);
        await context.SaveChangesAsync(cancellationToken);
        return membership.Id;
    }
}
