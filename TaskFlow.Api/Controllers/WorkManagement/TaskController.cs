using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Models.Requests;
using TaskFlow.Api.Models.Responses;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.AssignTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.AssignTaskToTeam;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CompleteTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CreateTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.DeleteTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.ReopenTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.StartTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.UnassignTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.UpdateTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetMyPersonalTasks;
using TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetMyTasks;
using TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetOrganizationTasks;
using TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetProjectTasks;
using TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetTaskById;

namespace TaskFlow.Api.Controllers.WorkManagement
{
    [Authorize(Policy = AuthorizationPolicies.AllRoles)]
    [Route("api/[controller]")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly IMediator _mediator;

        public TaskController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Creates an <b>organization</b> task. OrganizationId is required —
        /// use <c>POST /task/personal</c> for a personal task. Rejecting a
        /// missing id here is deliberate: silently falling back to a personal
        /// task would hide a client bug as invisible data corruption.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateTask(
            CreateTaskCommand command,
            CancellationToken cancellationToken)
        {
            if (!command.OrganizationId.HasValue)
            {
                return BadRequest(new ApiErrorResponse
                {
                    Code = "ORGANIZATION_ID_REQUIRED",
                    Message =
                        "OrganizationId is required. Use POST /api/task/personal " +
                        "to create a personal task.",
                    FailureReason =
                        Models.Enums.FailureReason.ValidationFailure.ToString(),
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var taskId =
                await _mediator.Send(
                    command,
                    cancellationToken);

            return Ok(taskId);
        }

        /// <summary>
        /// Creates a <b>personal</b> task for the signed-in user (Individual
        /// account). The task has no organization and may optionally belong to
        /// a private project owned by the same user. The creator comes from the JWT.
        /// </summary>
        [HttpPost("personal")]
        public async Task<IActionResult> CreatePersonalTask(
            CreatePersonalTaskRequest request,
            CancellationToken cancellationToken)
        {
            var taskId =
                await _mediator.Send(
                    new CreateTaskCommand(
                        request.Title,
                        request.Description,
                        request.StartDate,
                        request.Priority,
                        OrganizationId: null,
                        request.ExpectedCompletionDate,
                        request.ProjectId),
                    cancellationToken);

            return Ok(taskId);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateTask(
    UpdateTaskCommand command,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }
        [HttpDelete("{taskId:int}")]
        public async Task<IActionResult> DeleteTask(
    int taskId,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new DeleteTaskCommand(taskId),
                cancellationToken);

            return NoContent();
        }

        [HttpPut("{taskId:int}/start")]
        public async Task<IActionResult> StartTask(
    int taskId,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new StartTaskCommand(taskId),
                cancellationToken);

            return NoContent();
        }
        [HttpPut("{taskId:int}/complete")]
        public async Task<IActionResult> CompleteTask(
    int taskId,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new CompleteTaskCommand(taskId),
                cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Sends a completed task back to an open state — the "reopen" half of
        /// the documented lifecycle. If the task has subtasks, their state
        /// decides the resulting status.
        /// </summary>
        [HttpPut("{taskId:int}/reopen")]
        public async Task<IActionResult> ReopenTask(
            int taskId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new ReopenTaskCommand(taskId),
                cancellationToken);

            return NoContent();
        }

        [HttpPut("{taskId:int}/assign/{assignedToUserId:int}")]
        public async Task<IActionResult> AssignTask(
            int taskId,
            int assignedToUserId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new AssignTaskCommand(taskId, assignedToUserId),
                cancellationToken);

            return NoContent();
        }

        [HttpPut("{taskId:int}/unassign")]
        public async Task<IActionResult> UnassignTask(
            int taskId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new UnassignTaskCommand(taskId),
                cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Puts the task under a team, so it shows up in
        /// <c>GET /team/{teamId}/tasks</c>. Deliberately its own route
        /// rather than a field on <c>PUT /task</c> — see
        /// <see cref="AssignTaskToTeamCommand"/> for why.
        /// </summary>
        [HttpPut("{taskId:int}/team/{teamId:int}")]
        public async Task<IActionResult> AssignTaskToTeam(
            int taskId,
            int teamId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new AssignTaskToTeamCommand(taskId, teamId),
                cancellationToken);

            return NoContent();
        }

        /// <summary>
        /// Removes the task from its team. The task itself is
        /// untouched — only the team link is cleared.
        /// </summary>
        [HttpDelete("{taskId:int}/team")]
        public async Task<IActionResult> RemoveTaskFromTeam(
            int taskId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new AssignTaskToTeamCommand(taskId, null),
                cancellationToken);

            return NoContent();
        }

        [HttpGet("{taskId:int}")]
        public async Task<IActionResult> GetById(
            int taskId,
            CancellationToken cancellationToken)
        {
            var task =
                await _mediator.Send(
                    new GetTaskByIdQuery(taskId),
                    cancellationToken);

            return Ok(task);
        }

        [HttpGet("organization/{organizationId:int}")]
        public async Task<IActionResult> GetByOrganization(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var tasks =
                await _mediator.Send(
                    new GetOrganizationTasksQuery(organizationId),
                    cancellationToken);

            return Ok(tasks);
        }

        [HttpGet("project/{projectId:int}")]
        public async Task<IActionResult> GetByProject(
            int projectId,
            CancellationToken cancellationToken)
        {
            var tasks =
                await _mediator.Send(
                    new GetProjectTasksQuery(projectId),
                    cancellationToken);

            return Ok(tasks);
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine(
            CancellationToken cancellationToken)
        {
            var tasks =
                await _mediator.Send(
                    new GetMyTasksQuery(),
                    cancellationToken);

            return Ok(tasks);
        }

        [HttpGet("mine/personal")]
        public async Task<IActionResult> GetMinePersonal(
            CancellationToken cancellationToken)
        {
            var tasks =
                await _mediator.Send(
                    new GetMyPersonalTasksQuery(),
                    cancellationToken);

            return Ok(tasks);
        }
    }
}
