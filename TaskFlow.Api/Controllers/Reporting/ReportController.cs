using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Reporting.Queries.GetDashboardSummary;
using TaskFlow.Application.Features.Reporting.Queries.GetMemberTaskReport;
using TaskFlow.Application.Features.Reporting.Queries.GetProjectReport;
using TaskFlow.Application.Features.Reporting.Queries.GetTeamPerformanceReport;

namespace TaskFlow.Api.Controllers.Reporting
{
    // Development stage: endpoints are open. Secure later with:
    // [Authorize(Policy = Constants.AuthorizationPolicies.ManagerAndAbove)]
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ReportController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("dashboard/{organizationId:int}")]
        public async Task<IActionResult> Dashboard(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var summary =
                await _mediator.Send(
                    new GetDashboardSummaryQuery(organizationId),
                    cancellationToken);

            return Ok(summary);
        }

        [HttpGet("member/{userId:int}")]
        public async Task<IActionResult> Member(
            int userId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken cancellationToken)
        {
            var report =
                await _mediator.Send(
                    new GetMemberTaskReportQuery(userId, from, to),
                    cancellationToken);

            return Ok(report);
        }

        [HttpGet("team/{organizationId:int}")]
        public async Task<IActionResult> TeamPerformance(
            int organizationId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken cancellationToken)
        {
            var report =
                await _mediator.Send(
                    new GetTeamPerformanceReportQuery(organizationId, from, to),
                    cancellationToken);

            return Ok(report);
        }

        [HttpGet("project/{projectId:int}")]
        public async Task<IActionResult> Project(
            int projectId,
            CancellationToken cancellationToken)
        {
            var report =
                await _mediator.Send(
                    new GetProjectReportQuery(projectId),
                    cancellationToken);

            return Ok(report);
        }
    }
}
