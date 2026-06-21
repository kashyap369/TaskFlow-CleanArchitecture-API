using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.UpdateSubTask
{
    public sealed record UpdateSubTaskCommand(
        int SubTaskId,
        string Title
    ) : IRequest;
}