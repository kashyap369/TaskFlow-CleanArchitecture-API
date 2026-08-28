using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Options;

namespace TaskFlow.Api.Filters;

public sealed class PlannerFeatureFilter(IOptionsMonitor<PlannerOptions> options)
    : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!options.CurrentValue.Enabled)
        {
            context.Result = new NotFoundObjectResult(new
            {
                code = "PLANNER_NOT_AVAILABLE",
                message = "Planner is not available in this environment."
            });
            return;
        }

        await next();
    }
}
