using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreateProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreatePersonalProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.ImportProjectPlan;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.DeleteProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.UpdateProject;
using TaskFlow.Application.Features.WorkManagement.Projects.Queries.GetOrganizationProjects;
using TaskFlow.Application.Features.WorkManagement.Projects.Queries.GetMyPersonalProjects;
using TaskFlow.Application.Features.WorkManagement.Projects.Queries.GetProjectById;

namespace TaskFlow.Api.Controllers.WorkManagement
{
    [Authorize(Policy = AuthorizationPolicies.AllRoles)]
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

        [HttpPost("personal")]
        public async Task<IActionResult> CreatePersonalProject(
            CreatePersonalProjectCommand command,
            CancellationToken cancellationToken)
        {
            var projectId = await _mediator.Send(command, cancellationToken);

            return Ok(projectId);
        }

        /// <summary>
        /// Creates a complete project hierarchy in one transaction. OrganizationId is null for a
        /// private personal project and set for an organization project.
        /// </summary>
        [HttpPost("plan-import")]
        public async Task<IActionResult> ImportProjectPlan(
            ImportProjectPlanCommand command,
            CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
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

        [HttpGet("mine/personal")]
        public async Task<IActionResult> GetMinePersonal(
            CancellationToken cancellationToken)
        {
            var projects = await _mediator.Send(
                new GetMyPersonalProjectsQuery(),
                cancellationToken);

            return Ok(projects);
        }
    }
}
