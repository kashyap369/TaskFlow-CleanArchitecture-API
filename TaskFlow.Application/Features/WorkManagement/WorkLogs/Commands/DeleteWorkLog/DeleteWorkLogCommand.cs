using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.DeleteWorkLog
{
    public sealed record DeleteWorkLogCommand(
        int WorkLogId
    ) : IRequest;
}
