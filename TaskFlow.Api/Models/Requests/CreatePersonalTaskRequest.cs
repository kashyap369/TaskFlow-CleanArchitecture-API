using TaskFlow.Domain.Enums.WorkManagement;

namespace TaskFlow.Api.Models.Requests
{
    /// <summary>
    /// Body for <c>POST /api/task/personal</c>. Deliberately has no
    /// OrganizationId and no ProjectId: a personal task belongs to the
    /// signed-in user alone, and projects only exist inside organizations.
    /// The creator is taken from the JWT, never from the body.
    /// </summary>
    public sealed record CreatePersonalTaskRequest(
        string Title,
        string Description,
        DateTime StartDate,
        TaskPriority Priority,
        DateTime? ExpectedCompletionDate);
}
