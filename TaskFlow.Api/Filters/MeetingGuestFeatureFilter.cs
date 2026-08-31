using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TaskFlow.Infra.Meetings;

namespace TaskFlow.Api.Filters;

public sealed class MeetingGuestFeatureFilter(IOptionsMonitor<MeetingSettings> options) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!options.CurrentValue.Enabled || !options.CurrentValue.GuestsEnabled)
        {
            context.Result = new NotFoundObjectResult(new { code = "MEETING_GUESTS_NOT_AVAILABLE", message = "Meeting guest access is not available." });
            return;
        }
        await next();
    }
}
