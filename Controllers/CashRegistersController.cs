using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesSaaS.Domain;
using SalesSaaS.Features.CashRegisters;

namespace SalesSaaS.Controllers;

[ApiController]
[Route("api/cash-registers")]
[Authorize(Roles = Roles.Administration)]
public sealed class CashRegistersController(IMediator mediator) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<IActionResult> Open(OpenCashRegisterSessionCommand command) =>
        Created($"/api/cash-registers/sessions/{await mediator.Send(command)}", null);

    [HttpPost("sessions/{cashRegisterSessionId:guid}/movements")]
    public async Task<IActionResult> RecordMovement(Guid cashRegisterSessionId, RecordCashMovementCommand command)
    {
        if (cashRegisterSessionId != command.CashRegisterSessionId) return BadRequest("El identificador de la sesión no coincide.");
        return Created($"/api/cash-registers/movements/{await mediator.Send(command)}", null);
    }

    [HttpPost("sessions/{cashRegisterSessionId:guid}/close")]
    public async Task<IActionResult> Close(Guid cashRegisterSessionId, CloseCashRegisterSessionCommand command)
    {
        if (cashRegisterSessionId != command.CashRegisterSessionId) return BadRequest("El identificador de la sesión no coincide.");
        await mediator.Send(command);
        return NoContent();
    }
}
