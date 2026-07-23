using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Organizations.Organization.Commands.DeleteOrganization;
using TaskFlow.Application.Features.Organizations.Organization.Commands.CreateOrganization;
using TaskFlow.Application.Features.Organizations.Organization.Commands.UpdateOraganization;
using TaskFlow.Application.Features.Organizations.Organization.Queries.GetMyOrganizations;
using TaskFlow.Application.Features.Organizations.Organization.Queries.GetOrganizationById;

namespace TaskFlow.Api.Controllers.Organization
{
    // Development stage: endpoints are open. Secure later with:
    // [Authorize(Policy = Constants.AuthorizationPolicies.ManagerAndAbove)]
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

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine(
            CancellationToken cancellationToken)
        {
            var organizations =
                await _mediator.Send(
                    new GetMyOrganizationsQuery(),
                    cancellationToken);

            return Ok(organizations);
        }

        [HttpGet("{organizationId:int}")]
        public async Task<IActionResult> GetById(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var organization =
                await _mediator.Send(
                    new GetOrganizationByIdQuery(organizationId),
                    cancellationToken);

            return Ok(organization);
        }
    }
}