using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.CompleteSubTask;
using TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.CreateSubTask;
using TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.DeleteSubTask;
using TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.ReOpenSubTask;
using TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.UpdateSubTask;

namespace TaskFlow.Api.Controllers.WorkManagement
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubTaskController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SubTaskController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> CreateSubTask(
            CreateSubTaskCommand command,
            CancellationToken cancellationToken)
        {
            var subTaskId =
                await _mediator.Send(
                    command,
                    cancellationToken);

            return Ok(subTaskId);
        }
        [HttpPut]
        public async Task<IActionResult> UpdateSubTask(
    UpdateSubTaskCommand command,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }

        [HttpDelete("{subTaskId:int}")]
        public async Task<IActionResult> DeleteSubTask(
    int subTaskId,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new DeleteSubTaskCommand(subTaskId),
                cancellationToken);

            return NoContent();
        }
        [HttpPut("{subTaskId:int}/complete")]
        public async Task<IActionResult> CompleteSubTask(
    int subTaskId,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new CompleteSubTaskCommand(subTaskId),
                cancellationToken);

            return NoContent();
        }

        [HttpPut("{subTaskId:int}/reopen")]
        public async Task<IActionResult> ReOpenSubTask(
    int subTaskId,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new ReOpenSubTaskCommand(subTaskId),
                cancellationToken);

            return NoContent();
        }

    }
}