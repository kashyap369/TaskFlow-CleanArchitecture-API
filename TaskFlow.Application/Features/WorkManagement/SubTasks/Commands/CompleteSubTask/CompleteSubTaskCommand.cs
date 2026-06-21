using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.CompleteSubTask
{
    public sealed record CompleteSubTaskCommand(
        int SubTaskId
    ) : IRequest;
}