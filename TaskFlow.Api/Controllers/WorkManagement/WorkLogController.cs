using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.DeleteWorkLog;
using TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.LogManualWork;
using TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.StartWorkLog;
using TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.StopWorkLog;
using TaskFlow.Application.Features.WorkManagement.WorkLogs.Queries.GetMyWorkLogs;
using TaskFlow.Application.Features.WorkManagement.WorkLogs.Queries.GetTaskWorkLogs;

namespace TaskFlow.Api.Controllers.WorkManagement
{
    [Authorize(Policy = AuthorizationPolicies.AllRoles)]
    [Route("api/[controller]")]
    [ApiController]
    public class WorkLogController : ControllerBase
    {
        private readonly IMediator _mediator;

        public WorkLogController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("start")]
        public async Task<IActionResult> Start(
            StartWorkLogCommand command,
            CancellationToken cancellationToken)
        {
            var workLogId =
                await _mediator.Send(command, cancellationToken);

            return Ok(workLogId);
        }

        [HttpPut("stop")]
        public async Task<IActionResult> Stop(
            StopWorkLogCommand command,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(command, cancellationToken);

            return NoContent();
        }

        [HttpPost("manual")]
        public async Task<IActionResult> LogManual(
            LogManualWorkCommand command,
            CancellationToken cancellationToken)
        {
            var workLogId =
                await _mediator.Send(command, cancellationToken);

            return Ok(workLogId);
        }

        [HttpDelete("{workLogId:int}")]
        public async Task<IActionResult> Delete(
            int workLogId,
            CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new DeleteWorkLogCommand(workLogId),
                cancellationToken);

            return NoContent();
        }

        [HttpGet("task/{taskId:int}")]
        public async Task<IActionResult> GetByTask(
            int taskId,
            CancellationToken cancellationToken)
        {
            var logs =
                await _mediator.Send(
                    new GetTaskWorkLogsQuery(taskId),
                    cancellationToken);

            return Ok(logs);
        }

        [HttpGet("mine")]
        public async Task<IActionResult> GetMine(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            CancellationToken cancellationToken)
        {
            var logs =
                await _mediator.Send(
                    new GetMyWorkLogsQuery(from, to),
                    cancellationToken);

            return Ok(logs);
        }
    }
}
