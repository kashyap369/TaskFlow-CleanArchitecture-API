using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.UnassignTask
{
    public sealed record UnassignTaskCommand(
        int TaskId
    ) : IRequest;
}
