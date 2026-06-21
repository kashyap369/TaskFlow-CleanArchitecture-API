using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.ReOpenSubTask
{
    public sealed record ReOpenSubTaskCommand(
        int SubTaskId
    ) : IRequest;
}