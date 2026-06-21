using MediatR;
using TaskFlow.Domain.Enums.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.UpdateTask
{
    public sealed record UpdateTaskCommand(
        int TaskId,
        string Title,
        string Description,
        TaskPriority Priority,
        DateTime? ExpectedCompletionDate
    ) : IRequest;
}