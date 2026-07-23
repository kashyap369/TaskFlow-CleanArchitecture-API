using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.AcceptInvitation;
using TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.CancelInvitation;
using TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.InviteUser;
using TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.RejectInvitation;
using TaskFlow.Application.Features.Organizations.OrganizationInvitation.Queries.GetMyInvitations;
using TaskFlow.Application.Features.Organizations.OrganizationInvitation.Queries.GetOrganizationInvitations;

namespace TaskFlow.Api.Controllers.Organization
{
    // Any authenticated user; invite is gated by the InviteMember
    // org permission in the handler, and accept/reject/cancel act
    // on the current user's own invitations.
    [Authorize(Policy = AuthorizationPolicies.AllRoles)]
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationInvitationController : ControllerBase
    {
        private readonly IMediator _mediator;

        public OrganizationInvitationController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("invite")]
        public async Task<IActionResult> Invite(
            InviteUserCommand command,
            CancellationToken cancellationToken)
        {
            var invitationId =
                await _mediator.Send(
                    command,
                    cancellationToken);

            return Ok(invitationId);
        }

        [HttpPost("accept")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Accept(
      AcceptInvitationCommand command,
      CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }
        [HttpPost("reject")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Reject(
        RejectInvitationCommand command,
        CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }

        [HttpPost("cancel")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Cancel(
    CancelInvitationCommand command,
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
            var invitations =
                await _mediator.Send(
                    new GetOrganizationInvitationsQuery(organizationId),
                    cancellationToken);

            return Ok(invitations);
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine(
            CancellationToken cancellationToken)
        {
            var invitations =
                await _mediator.Send(
                    new GetMyInvitationsQuery(),
                    cancellationToken);

            return Ok(invitations);
        }
    }
}