using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ActivateMember;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ChangeMemberRole;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.DeactivateMember;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.RemoveMember;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Queries.GetOrganizationMembers;

namespace TaskFlow.Api.Controllers.Organization
{
    // Any authenticated user; org-level authorization is enforced
    // inside the handlers.
    [Authorize(Policy = AuthorizationPolicies.AllRoles)]
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationMemberController : ControllerBase
    {
        private readonly IMediator _mediator;

        public OrganizationMemberController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveMember(
            RemoveMemberCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }
        [HttpPut("change-role")]
        public async Task<IActionResult> ChangeRole(
    ChangeMemberRoleCommand command,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }
        [HttpPut("deactivate")]
        public async Task<IActionResult> DeactivateMember(
    DeactivateMemberCommand command,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }
        [HttpPut("activate")]
        public async Task<IActionResult> ActivateMember(
    ActivateMemberCommand command,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }

        [HttpGet("organization/{organizationId:int}")]
        public async Task<IActionResult> GetByOrganization(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var members =
                await _mediator.Send(
                    new GetOrganizationMembersQuery(organizationId),
                    cancellationToken);

            return Ok(members);
        }
    }
}