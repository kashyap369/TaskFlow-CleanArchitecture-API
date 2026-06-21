using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.DeleteTask
{
    public sealed record DeleteTaskCommand(
        int TaskId
    ) : IRequest;
}