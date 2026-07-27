using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.ReopenTask
{
    /// <summary>
    /// Sends a completed task back to an open state, completing the
    /// documented lifecycle Todo → InProgress → Completed → reopen.
    /// </summary>
    public sealed record ReopenTaskCommand(
        int TaskId
    ) : IRequest;
}
