using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ActivateMember;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ChangeMemberRole;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.DeactivateMember;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.RemoveMember;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.SetMemberCapacity;
using TaskFlow.Api.Models.Requests;
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

        /// <summary>Sets or clears a member's normal Monday-Sunday working capacity.</summary>
        [HttpPut("organization/{organizationId:int}/users/{userId:int}/capacity")]
        public async Task<IActionResult> SetCapacity(
            int organizationId,
            int userId,
            SetMemberCapacityRequest request,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new SetMemberCapacityCommand(
                    organizationId,
                    userId,
                    request.WeeklyCapacityMinutes),
                cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// The organization's members. Both filters are optional:
        /// <c>?organizationRoleId=</c> narrows to one org role (this is
        /// what makes assignment "optionally filtered role-wise"), and
        /// <c>?activeOnly=true</c> drops deactivated members — what an
        /// assignee picker wants. Omitting both returns every member,
        /// exactly as before.
        /// </summary>
        [HttpGet("organization/{organizationId:int}")]
        public async Task<IActionResult> GetByOrganization(
            int organizationId,
            CancellationToken cancellationToken,
            [FromQuery] int? organizationRoleId = null,
            [FromQuery] bool activeOnly = false)
        {
            var members =
                await _mediator.Send(
                    new GetOrganizationMembersQuery(
                        organizationId,
                        organizationRoleId,
                        activeOnly),
                    cancellationToken);

            return Ok(members);
        }
    }
}
