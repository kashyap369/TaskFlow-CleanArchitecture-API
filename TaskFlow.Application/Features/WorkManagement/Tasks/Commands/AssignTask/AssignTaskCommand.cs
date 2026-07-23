using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.AssignTask
{
    public sealed record AssignTaskCommand(
        int TaskId,
        int AssignedToUserId
    ) : IRequest;
}
