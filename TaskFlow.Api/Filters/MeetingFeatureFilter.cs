using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TaskFlow.Infra.Meetings;

namespace TaskFlow.Api.Filters;

public sealed class MeetingFeatureFilter(IOptionsMonitor<MeetingSettings> options) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!options.CurrentValue.Enabled)
        {
            context.Result = new NotFoundObjectResult(new
            { code = "MEETINGS_NOT_AVAILABLE", message = "Meetings are not available in this environment." });
            return;
        }
        await next();
    }
}
