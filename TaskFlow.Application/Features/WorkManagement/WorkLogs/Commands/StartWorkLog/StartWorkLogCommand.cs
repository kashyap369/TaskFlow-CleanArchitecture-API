using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.StartWorkLog
{
    public sealed record StartWorkLogCommand(
        int TaskId,
        string? Notes
    ) : IRequest<int>;
}
