using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.Calendar;

namespace TaskFlow.Api.Controllers.Organization;

[Authorize(Policy = AuthorizationPolicies.AllRoles)]
[Route("api/calendar")]
[ApiController]
public sealed class CalendarEntryController : ControllerBase
{
    private readonly IMediator _mediator;
    public CalendarEntryController(IMediator mediator) => _mediator = mediator;

    [HttpGet("organization/{organizationId:int}")]
    public async Task<IActionResult> Get(int organizationId, [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetCalendarEntriesQuery(organizationId, fromUtc, toUtc), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateCalendarEntryCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update(UpdateCalendarEntryCommand command, CancellationToken cancellationToken)
    { await _mediator.Send(command, cancellationToken); return NoContent(); }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    { await _mediator.Send(new DeleteCalendarEntryCommand(id), cancellationToken); return NoContent(); }
}
