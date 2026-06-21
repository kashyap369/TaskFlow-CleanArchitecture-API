using MediatR;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreateProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.DeleteProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.UpdateProject;

namespace TaskFlow.Api.Controllers.WorkManagement
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProjectController(
            IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject(
            CreateProjectCommand command,
            CancellationToken cancellationToken)
        {
            var projectId =
                await _mediator.Send(
                    command,
                    cancellationToken);

            return Ok(projectId);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateProject(
    UpdateProjectCommand command,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                command,
                cancellationToken);

            return NoContent();
        }
        [HttpDelete("{projectId:int}")]
        public async Task<IActionResult> DeleteProject(
    int projectId,
    CancellationToken cancellationToken)
        {
            await _mediator.Send(
                new DeleteProjectCommand(projectId),
                cancellationToken);

            return NoContent();
        }

    }
}