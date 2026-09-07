using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Authentication.Commands;

public record LoginCommand(string Email, string Password, Guid? TenantId = null) : IRequest<AuthResponse>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(
    ApplicationDbContext context,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService,
    IRefreshTokenService refreshTokenService) : IRequestHandler<LoginCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await context.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Correo electrónico o contraseña incorrectos.");
        }

        var memberships = context.TenantMemberships.Where(membership => membership.UserId == user.Id && membership.IsActive);
        var membership = request.TenantId.HasValue
            ? await memberships.SingleOrDefaultAsync(candidate => candidate.TenantId == request.TenantId, cancellationToken)
            : await memberships.OrderBy(candidate => candidate.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a un negocio activo.");
        }

        var refreshToken = refreshTokenService.Create(user.Id, membership.TenantId);
        context.RefreshTokens.Add(refreshToken.Entity);
        await context.SaveChangesAsync(cancellationToken);

        return AuthResponse.From(tokenService.Create(user, membership), refreshToken, user, membership);
    }
}
