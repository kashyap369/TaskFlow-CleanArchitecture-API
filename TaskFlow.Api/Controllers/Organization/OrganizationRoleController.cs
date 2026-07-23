using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.CreateRole;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.DeleteRole;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.GrantPermission;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.RevokePermission;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.UpdateRole;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Queries.GetOrganizationRoles;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Queries.GetPermissions;
using TaskFlow.Application.Features.Organizations.OrganizationRole.Queries.GetRoleById;

namespace TaskFlow.Api.Controllers.Organization
{
    // Development stage: endpoints are open. Secure later with:
    // [Authorize(Policy = Constants.AuthorizationPolicies.AdminOnly)]
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

        [HttpPost("grant-permission")]
        public async Task<IActionResult> GrantPermission(
            GrantPermissionCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);

            return NoContent();
        }

        [HttpPost("revoke-permission")]
        public async Task<IActionResult> RevokePermission(
            RevokePermissionCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);

            return NoContent();
        }

        [HttpGet("organization/{organizationId:int}")]
        public async Task<IActionResult> GetByOrganization(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var roles =
                await _mediator.Send(
                    new GetOrganizationRolesQuery(organizationId),
                    cancellationToken);

            return Ok(roles);
        }

        [HttpGet("{roleId:int}")]
        public async Task<IActionResult> GetById(
            int roleId,
            CancellationToken cancellationToken)
        {
            var role =
                await _mediator.Send(
                    new GetRoleByIdQuery(roleId),
                    cancellationToken);

            return Ok(role);
        }

        // The organization permission catalog (grantable permissions).
        [HttpGet("permissions")]
        public async Task<IActionResult> GetPermissions(
            CancellationToken cancellationToken)
        {
            var permissions =
                await _mediator.Send(
                    new GetPermissionsQuery(),
                    cancellationToken);

            return Ok(permissions);
        }
    }
}