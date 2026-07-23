using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.AssignTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CompleteTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CreateTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.DeleteTask;
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

        [HttpPost]
        public async Task<IActionResult> CreateTask(
            CreateTaskCommand command,
            CancellationToken cancellationToken)
        {
            var taskId =
                await _mediator.Send(
                    command,
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