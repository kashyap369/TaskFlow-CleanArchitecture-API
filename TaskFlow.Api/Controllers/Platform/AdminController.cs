using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Constants;
using TaskFlow.Api.Filters;
using TaskFlow.Application.Features.Organizations.Organization.Queries.GetAllOrganizations;
using TaskFlow.Application.Features.Platform.Commands.UpdatePlatformSettings;
using TaskFlow.Application.Features.Platform.Queries.GetPlatformSettings;
using TaskFlow.Application.Features.Planner.Commands.ManagePlannerTemplate;
using TaskFlow.Application.Features.Planner.Queries.GetPlannerTemplates;

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

        [HttpGet("planner/templates")]
        [ServiceFilter(typeof(PlannerFeatureFilter))]
        [EnableRateLimiting("planner")]
        public async Task<IActionResult> GetPlannerTemplates(CancellationToken cancellationToken) =>
            Ok(await _mediator.Send(new GetPlannerTemplatesQuery(true), cancellationToken));

        [HttpPost("planner/templates")]
        [ServiceFilter(typeof(PlannerFeatureFilter))]
        [EnableRateLimiting("planner")]
        public async Task<IActionResult> CreatePlannerTemplate(PlannerTemplateDefinition definition, CancellationToken cancellationToken) =>
            Ok(await _mediator.Send(new CreatePlannerTemplateCommand(definition), cancellationToken));

        [HttpPut("planner/templates/{templateId:guid}")]
        [ServiceFilter(typeof(PlannerFeatureFilter))]
        [EnableRateLimiting("planner")]
        public async Task<IActionResult> UpdatePlannerTemplate(Guid templateId, PlannerTemplateDefinition definition, CancellationToken cancellationToken) =>
            Ok(await _mediator.Send(new UpdatePlannerTemplateCommand(templateId, definition), cancellationToken));

        [HttpPost("planner/templates/{templateId:guid}/publish")]
        [ServiceFilter(typeof(PlannerFeatureFilter))]
        [EnableRateLimiting("planner")]
        public async Task<IActionResult> PublishPlannerTemplate(Guid templateId, CancellationToken cancellationToken) =>
            Ok(await _mediator.Send(new PublishPlannerTemplateCommand(templateId), cancellationToken));

        [HttpPost("planner/templates/{templateId:guid}/archive")]
        [ServiceFilter(typeof(PlannerFeatureFilter))]
        [EnableRateLimiting("planner")]
        public async Task<IActionResult> ArchivePlannerTemplate(Guid templateId, CancellationToken cancellationToken)
        {
            await _mediator.Send(new ArchivePlannerTemplateCommand(templateId), cancellationToken);
            return NoContent();
        }
    }
}
