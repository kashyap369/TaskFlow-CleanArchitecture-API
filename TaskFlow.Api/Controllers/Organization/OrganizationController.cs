using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Organizations.Organization.Commands.DeleteOrganization;
using TaskFlow.Application.Features.Organizations.Organization.Commands.CreateOrganization;
using TaskFlow.Application.Features.Organizations.Organization.Commands.UpdateOraganization;

namespace TaskFlow.Api.Controllers.Organization
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationController : ControllerBase
    {
        private readonly IMediator _mediator;

        public OrganizationController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        public async Task<IActionResult> Create(
            CreateOrganizationCommand command,
            CancellationToken cancellationToken)
        {
            var organizationId =
                await _mediator.Send(
                    command,
                    cancellationToken);

            return Ok(organizationId);
        }

        [HttpPut]
        public async Task<IActionResult> Update(UpdateOrganizationCommand command,CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }
        [HttpDelete("{organizationId:int}")]
        public async Task<IActionResult> Delete(int organizationId,CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new DeleteOrganizationCommand(
                    organizationId),
                cancellationToken);

            return NoContent();
        }


    }
}