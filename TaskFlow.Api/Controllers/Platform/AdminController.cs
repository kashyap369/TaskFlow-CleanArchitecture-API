using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.Organizations.Organization.Queries.GetAllOrganizations;
using TaskFlow.Application.Features.Platform.Commands.UpdatePlatformSettings;
using TaskFlow.Application.Features.Platform.Queries.GetPlatformSettings;

namespace TaskFlow.Api.Controllers.Platform
{
    /// <summary>
    /// Platform administration. Every route here is
    /// <b>AdminOnly</b> — this is the one controller whose data is
    /// not scoped to an organization, so the usual owner/member
    /// access guard does not apply and the system role is the whole
    /// authorization story.
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Every organization on the platform. Distinct from
        /// <c>GET /organization/mine</c>, which returns only the
        /// caller's own — an admin belongs to no organization, so
        /// that route returns an empty list for them.
        /// </summary>
        [HttpGet("organizations")]
        public async Task<IActionResult> GetOrganizations(
            CancellationToken cancellationToken)
        {
            var organizations =
                await _mediator.Send(
                    new GetAllOrganizationsQuery(),
                    cancellationToken);

            return Ok(organizations);
        }

        [HttpGet("settings")]
        public async Task<IActionResult> GetSettings(
            CancellationToken cancellationToken)
        {
            var settings =
                await _mediator.Send(
                    new GetPlatformSettingsQuery(),
                    cancellationToken);

            return Ok(settings);
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettings(
            UpdatePlatformSettingsCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }
    }
}
