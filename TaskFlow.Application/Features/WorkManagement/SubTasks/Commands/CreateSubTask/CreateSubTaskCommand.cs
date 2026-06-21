using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.CreateSubTask
{
    public sealed record CreateSubTaskCommand(
        string Title,
        int TaskId
    ) : IRequest<int>;
}