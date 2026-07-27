using MediatR;
using TaskFlow.Domain.Enums.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CreateTask
{
    /// <summary>
    /// Creates a task. <c>OrganizationId</c> is <b>null for a personal task</b>
    /// (Individual account) and set for an organization task. A personal task
    /// can never belong to a project or a team, because both only exist inside
    /// organizations.
    ///
    /// <c>TeamId</c> is optional even for an organization task — it names the
    /// team responsible, which is what makes "tasks viewed per team" possible.
    /// It is last and defaulted so every existing caller keeps compiling and
    /// every existing client keeps working.
    /// </summary>
    public sealed record CreateTaskCommand(
        string Title,
        string Description,
        DateTime StartDate,
        TaskPriority Priority,
        int? OrganizationId,
        DateTime? ExpectedCompletionDate,
        int? ProjectId,
        int? TeamId = null
    ) : IRequest<int>;
}