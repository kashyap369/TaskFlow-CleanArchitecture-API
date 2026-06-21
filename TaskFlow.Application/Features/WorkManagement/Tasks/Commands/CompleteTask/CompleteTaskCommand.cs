using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CompleteTask
{
    public sealed record CompleteTaskCommand(
        int TaskId
    ) : IRequest;
}