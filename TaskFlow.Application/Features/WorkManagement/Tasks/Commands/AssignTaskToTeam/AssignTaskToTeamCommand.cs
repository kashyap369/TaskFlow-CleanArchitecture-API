using MediatR;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.AssignTaskToTeam
{
    /// <summary>
    /// Moves a task to a team, or clears its team when
    /// <paramref name="TeamId"/> is null.
    ///
    /// This is deliberately <b>not</b> a field on
    /// <c>UpdateTaskCommand</c>. That command is filled from an edit
    /// form, and the task list DTO would not have carried the team —
    /// so a client that saved the form would silently clear the team
    /// of every task it touched. The same trap has already bitten this
    /// project twice (task description, organization description).
    /// A dedicated command cannot be invoked by accident, and it
    /// matches how assign / unassign already work.
    /// </summary>
    public sealed record AssignTaskToTeamCommand(
        int TaskId,
        int? TeamId
    ) : IRequest;
}
