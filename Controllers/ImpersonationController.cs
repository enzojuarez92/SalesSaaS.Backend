using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesSaaS.Application.Security;
using SalesSaaS.Infrastructure;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/impersonation")]
[Authorize]
public sealed class ImpersonationController(ApplicationDbContext context, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("stop")]
    public async Task<IActionResult> Stop(CancellationToken cancellationToken)
    {
        if (!currentUser.SupportImpersonationLogId.HasValue || !currentUser.ImpersonatorUserId.HasValue || !currentUser.UserId.HasValue || !currentUser.TenantId.HasValue)
            return BadRequest(new { message = "No hay una sesión de soporte activa." });

        var log = await context.SupportImpersonationLogs.SingleOrDefaultAsync(item => item.Id == currentUser.SupportImpersonationLogId
            && item.SuperAdminUserId == currentUser.ImpersonatorUserId
            && item.ImpersonatedUserId == currentUser.UserId
            && item.TenantId == currentUser.TenantId
            && item.EndedAtUtc == null, cancellationToken);
        if (log is null) return NoContent();

        log.EndedAtUtc = DateTime.UtcNow;
        var refreshTokens = await context.RefreshTokens.IgnoreQueryFilters()
            .Where(item => item.SupportImpersonationLogId == log.Id && item.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in refreshTokens) refreshToken.RevokedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
