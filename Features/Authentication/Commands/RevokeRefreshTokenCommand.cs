using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Features.Authentication.Commands;

public sealed record RevokeRefreshTokenCommand(string RefreshToken) : IRequest;

public sealed class RevokeRefreshTokenCommandValidator : AbstractValidator<RevokeRefreshTokenCommand>
{
    public RevokeRefreshTokenCommandValidator() =>
        RuleFor(command => command.RefreshToken).NotEmpty().MaximumLength(512);
}

public sealed class RevokeRefreshTokenCommandHandler(
    ApplicationDbContext context,
    IRefreshTokenService refreshTokenService) : IRequestHandler<RevokeRefreshTokenCommand>
{
    public async Task Handle(RevokeRefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = refreshTokenService.Hash(request.RefreshToken);
        var storedToken = await context.RefreshTokens
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is not null && storedToken.RevokedAtUtc is null)
        {
            storedToken.RevokedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
