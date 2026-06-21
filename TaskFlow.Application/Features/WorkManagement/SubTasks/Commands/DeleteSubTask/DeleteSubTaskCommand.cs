using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.DeleteSubTask
{
    public sealed record DeleteSubTaskCommand(
        int SubTaskId
    ) : IRequest;
}