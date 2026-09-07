using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Authentication.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty().MaximumLength(512);
    }
}

public sealed class RefreshTokenCommandHandler(
    ApplicationDbContext context,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService) : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = refreshTokenService.Hash(request.RefreshToken);
        var storedToken = await context.RefreshTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.RevokedAtUtc is not null || storedToken.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("El token de actualización no es válido o expiró.");
        }

        var user = await context.Users.SingleOrDefaultAsync(candidate => candidate.Id == storedToken.UserId, cancellationToken);
        var membership = await context.TenantMemberships
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(candidate => candidate.UserId == storedToken.UserId
                && candidate.TenantId == storedToken.TenantId
                && candidate.IsActive, cancellationToken);

        if (user is null || !user.IsActive || membership is null)
        {
            throw new UnauthorizedAccessException("El usuario o su acceso al negocio ya no están activos.");
        }

        var replacement = refreshTokenService.Create(user.Id, membership.TenantId);
        storedToken.RevokedAtUtc = DateTime.UtcNow;
        storedToken.ReplacedByTokenId = replacement.Entity.Id;
        context.RefreshTokens.Add(replacement.Entity);
        await context.SaveChangesAsync(cancellationToken);

        return AuthResponse.From(jwtTokenService.Create(user, membership), replacement, user, membership);
    }
}
