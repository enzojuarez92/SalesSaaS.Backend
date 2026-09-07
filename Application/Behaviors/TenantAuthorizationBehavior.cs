using MediatR;
using SalesSaaS.Application.Exceptions;
using SalesSaaS.Application.Security;

namespace SalesSaaS.Application.Behaviors;

public sealed class TenantAuthorizationBehavior<TRequest, TResponse>(
    ICurrentUser currentUser) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is ITenantScopedRequest tenantRequest)
        {
            if (!currentUser.IsAuthenticated || currentUser.TenantId is null)
            {
                throw new UnauthorizedAccessException("Debés iniciar sesión para acceder a este negocio.");
            }

            if (tenantRequest.TenantId != currentUser.TenantId)
            {
                throw new ForbiddenAccessException("No tenés permiso para acceder a datos de otro negocio.");
            }
        }

        return await next();
    }
}
