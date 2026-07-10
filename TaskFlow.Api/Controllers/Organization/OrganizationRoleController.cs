using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.CreateRole;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.DeleteRole;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.UpdateRole;

namespace TaskFlow.Api.Controllers.Organization
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationRoleController : ControllerBase
    {
        private readonly IMediator _mediator;

        public OrganizationRoleController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole(
            CreateRoleCommand command,
            CancellationToken cancellationToken)
        {
            var roleId =
                await _mediator.Send(
                    command,
                    cancellationToken);

            return Ok(roleId);
        }
        [HttpPut]
        public async Task<IActionResult> UpdateRole(
    UpdateRoleCommand command,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }

        [HttpDelete("{roleId:int}")]
        public async Task<IActionResult> DeleteRole(
    int roleId,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new DeleteRoleCommand(roleId),
                cancellationToken);

            return NoContent();
        }


    }
}