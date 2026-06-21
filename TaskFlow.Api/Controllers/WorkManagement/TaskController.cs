using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CompleteTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CreateTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.DeleteTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.StartTask;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.UpdateTask;

namespace TaskFlow.Api.Controllers.WorkManagement
{
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

    }
}