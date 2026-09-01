using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using TaskFlow.Infra.Meetings;

namespace TaskFlow.Api.Filters;

public sealed class MeetingRecordingFeatureFilter(IOptionsMonitor<MeetingSettings> options) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!options.CurrentValue.RecordingEnabled)
        { context.Result = new NotFoundObjectResult(new { code = "MEETING_RECORDING_DISABLED", message = "Meeting recording is not enabled." }); return; }
        await next();
    }
}
