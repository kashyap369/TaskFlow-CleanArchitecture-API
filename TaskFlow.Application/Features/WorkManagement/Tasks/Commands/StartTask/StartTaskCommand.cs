using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.StartTask
{
    public sealed record StartTaskCommand(
        int TaskId
    ) : IRequest;
}