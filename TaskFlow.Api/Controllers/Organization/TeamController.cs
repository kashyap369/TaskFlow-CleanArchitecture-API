using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.Organizations.Team.Commands.AddTeamMember;
using TaskFlow.Application.Features.Organizations.Team.Commands.CreateTeam;
using TaskFlow.Application.Features.Organizations.Team.Commands.DeleteTeam;
using TaskFlow.Application.Features.Organizations.Team.Commands.RemoveTeamMember;
using TaskFlow.Application.Features.Organizations.Team.Commands.UpdateTeam;
using TaskFlow.Application.Features.Organizations.Team.Queries.GetOrganizationTeams;
using TaskFlow.Application.Features.Organizations.Team.Queries.GetTeamById;

namespace TaskFlow.Api.Controllers.Organization
{
    // Any authenticated user; the ManageTeams org permission is
    // enforced in the command handlers via IOrganizationPermissionChecker.
    [Authorize(Policy = AuthorizationPolicies.AllRoles)]
    [Route("api/[controller]")]
    [ApiController]
    public class TeamController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TeamController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            CreateTeamCommand command,
            CancellationToken cancellationToken)
        {
            var teamId =
                await _mediator.Send(command, cancellationToken);

            return Ok(teamId);
        }

        [HttpPut]
        public async Task<IActionResult> Update(
            UpdateTeamCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);

            return NoContent();
        }

        [HttpDelete("{teamId:int}")]
        public async Task<IActionResult> Delete(
            int teamId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new DeleteTeamCommand(teamId),
                cancellationToken);

            return NoContent();
        }

        [HttpPost("{teamId:int}/members/{userId:int}")]
        public async Task<IActionResult> AddMember(
            int teamId,
            int userId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new AddTeamMemberCommand(teamId, userId),
                cancellationToken);

            return NoContent();
        }

        [HttpDelete("{teamId:int}/members/{userId:int}")]
        public async Task<IActionResult> RemoveMember(
            int teamId,
            int userId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new RemoveTeamMemberCommand(teamId, userId),
                cancellationToken);

            return NoContent();
        }

        [HttpGet("organization/{organizationId:int}")]
        public async Task<IActionResult> GetByOrganization(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var teams =
                await _mediator.Send(
                    new GetOrganizationTeamsQuery(organizationId),
                    cancellationToken);

            return Ok(teams);
        }

        [HttpGet("{teamId:int}")]
        public async Task<IActionResult> GetById(
            int teamId,
            CancellationToken cancellationToken)
        {
            var team =
                await _mediator.Send(
                    new GetTeamByIdQuery(teamId),
                    cancellationToken);

            return Ok(team);
        }
    }
}
