using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.TenantMemberships.Commands;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/tenant-members")]
[Authorize(Roles = Roles.Owner)]
public sealed class TenantMembersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateTenantMemberCommand command)
    {
        var membershipId = await mediator.Send(command);
        return Created($"/api/tenant-members/{membershipId}", new { id = membershipId });
    }
}
