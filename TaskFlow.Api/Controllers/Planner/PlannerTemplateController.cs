using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Filters;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.Planner.Queries.GetPlannerTemplates;

namespace TaskFlow.Api.Controllers.Planner;

[Authorize(Policy = AuthorizationPolicies.AllRoles)]
[Route("api/planner/templates")]
[ApiController]
[ServiceFilter(typeof(PlannerFeatureFilter))]
[EnableRateLimiting("planner")]
public sealed class PlannerTemplateController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPlannerTemplatesQuery(false), cancellationToken));
}
