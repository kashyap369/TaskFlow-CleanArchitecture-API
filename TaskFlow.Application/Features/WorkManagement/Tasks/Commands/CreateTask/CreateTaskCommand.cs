using MediatR;
using TaskFlow.Domain.Enums.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CreateTask
{
    public sealed record CreateTaskCommand(
        string Title,
        string Description,
        DateTime StartDate,
        TaskPriority Priority,
        int OrganizationId,
        int CreatedByUserId,
        DateTime? ExpectedCompletionDate,
        int? ProjectId
    ) : IRequest<int>;
}