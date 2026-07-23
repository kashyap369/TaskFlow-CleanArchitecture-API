using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreateProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.DeleteProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.UpdateProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Queries.GetOrganizationProjects;
using TaskFlow.Application.Features.WorkManagement.Projects.Queries.GetProjectById;

namespace TaskFlow.Api.Controllers.WorkManagement
{
    // Development stage: endpoints are open. Secure later with:
    // [Authorize(Policy = Constants.AuthorizationPolicies.ManagerAndAbove)]
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

        [HttpGet("{projectId:int}")]
        public async Task<IActionResult> GetById(
            int projectId,
            CancellationToken cancellationToken)
        {
            var project =
                await _mediator.Send(
                    new GetProjectByIdQuery(projectId),
                    cancellationToken);

            return Ok(project);
        }

        [HttpGet("organization/{organizationId:int}")]
        public async Task<IActionResult> GetByOrganization(
            int organizationId,
            CancellationToken cancellationToken)
        {
            var projects =
                await _mediator.Send(
                    new GetOrganizationProjectsQuery(organizationId),
                    cancellationToken);

            return Ok(projects);
        }
    }
}