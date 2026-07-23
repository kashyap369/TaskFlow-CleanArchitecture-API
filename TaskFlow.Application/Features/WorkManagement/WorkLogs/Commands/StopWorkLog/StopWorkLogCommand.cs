using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.StopWorkLog
{
    public sealed record StopWorkLogCommand(
        int WorkLogId,
        string? Notes
    ) : IRequest;
}
