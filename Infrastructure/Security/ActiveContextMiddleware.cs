using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Exceptions;
using SalesSaaS.Application.Security;
using SalesSaaS.Domain;

namespace SalesSaaS.Infrastructure.Security;

// A warehouse header selects a scope; database membership authorizes it.
public sealed class ActiveContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, ApplicationDbContext db, ICurrentUser user)
    {
        if (user.IsAuthenticated)
        {
            var membership = await db.TenantMemberships.AsNoTracking().Include(x => x.User)
                .SingleOrDefaultAsync(x => x.TenantId == user.TenantId && x.UserId == user.UserId, http.RequestAborted);
            var version = http.User.FindFirstValue("token_version") ?? "0";
            if (membership is null || !membership.IsActive || membership.User is not { IsActive: true }
                || membership.Role != http.User.FindFirstValue(ClaimTypes.Role)
                || version != membership.User.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
                throw new UnauthorizedAccessException("Tu acceso cambió o la sesión venció. Iniciá sesión nuevamente.");

            if (http.Request.Headers.TryGetValue("X-Warehouse-Id", out var header))
            {
                if (header.Count != 1 || !Guid.TryParse(header[0], out var warehouseId))
                    throw new InvalidOperationException("El depósito seleccionado no es válido.");
                if (!await db.Warehouses.AnyAsync(x => x.Id == warehouseId && x.TenantId == user.TenantId && x.IsActive, http.RequestAborted))
                    throw new ForbiddenAccessException("El depósito no pertenece al negocio activo o está inactivo.");
                if (membership.Role is not (Roles.Owner or Roles.Admin) && !await db.UserWarehouses.AnyAsync(x => x.UserId == user.UserId && x.TenantId == user.TenantId && x.WarehouseId == warehouseId, http.RequestAborted))
                    throw new ForbiddenAccessException("No tenés acceso al depósito seleccionado.");
                http.Items["WarehouseId"] = warehouseId;
            }
        }
        await next(http);
    }
}
