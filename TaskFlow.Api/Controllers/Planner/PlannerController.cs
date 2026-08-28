using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Constants;
using TaskFlow.Api.Filters;
using TaskFlow.Api.Models.Requests;
using TaskFlow.Application.Features.Planner.Commands.SavePlannerScene;
using TaskFlow.Application.Features.Planner.Queries.GetPlannerBoard;
using TaskFlow.Application.Features.Planner.Queries.GetPlannerSceneRevision;
using TaskFlow.Application.Features.Planner.Queries.GetPlannerSceneRevisions;
using TaskFlow.Application.Features.Planner.Queries.GetPlannerWorkspace;
using TaskFlow.Application.Features.Planner.Commands.LinkPlannerProject;
using TaskFlow.Application.Features.Planner.Commands.CreatePlannerTaskNode;
using TaskFlow.Application.Features.Planner.Commands.CreatePlannerSubTaskNode;
using TaskFlow.Application.Features.Planner.Commands.UpdatePlannerNode;
using TaskFlow.Application.Features.Planner.Commands.RemovePlannerNode;
using TaskFlow.Application.Features.Planner.Queries.GetPlannerTemplates;
using TaskFlow.Application.Features.Planner.Commands.ManagePlannerResource;
using TaskFlow.Application.Features.Planner.Queries.GetPlannerResources;
using TaskFlow.Application.Features.Planner.Commands.FinalizePrimaryRequirements;
using TaskFlow.Application.Features.Planner.Queries.GetRequirementBaselines;
using TaskFlow.Application.Features.Planner.Queries.GetRequirementBaseline;
using TaskFlow.Application.Features.Planner.Queries.GetRequirementChanges;
using TaskFlow.Application.Features.Planner.Queries.CompareRequirements;
using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Api.Controllers.Planner;

[Authorize(Policy = AuthorizationPolicies.AllRoles)]
[ServiceFilter(typeof(PlannerFeatureFilter))]
[EnableRateLimiting("planner")]
[Route("api/planner/projects/{projectId:int}/board")]
[ApiController]
public sealed class PlannerController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlannerController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetBoard(
        int projectId,
        CancellationToken cancellationToken)
    {
        var board = await _mediator.Send(
            new GetPlannerBoardQuery(projectId),
            cancellationToken);
        Response.Headers.ETag = $"\"{board.Revision}\"";
        return Ok(board);
    }

    [HttpPut("scene")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> SaveScene(
        int projectId,
        SavePlannerSceneRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SavePlannerSceneCommand(
                projectId,
                request.ExpectedRevision,
                request.SceneJson),
            cancellationToken);
        Response.Headers.ETag = $"\"{result.Revision}\"";
        return Ok(result);
    }

    [HttpGet("revisions")]
    public async Task<IActionResult> GetRevisions(
        int projectId,
        CancellationToken cancellationToken)
    {
        var revisions = await _mediator.Send(
            new GetPlannerSceneRevisionsQuery(projectId),
            cancellationToken);
        return Ok(revisions);
    }

    [HttpGet("revisions/{revision:int}")]
    public async Task<IActionResult> GetRevision(
        int projectId,
        int revision,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetPlannerSceneRevisionQuery(projectId, revision),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("workspace")]
    public async Task<IActionResult> GetWorkspace(int projectId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetPlannerWorkspaceQuery(projectId), cancellationToken));

    [HttpPost("nodes/project")]
    public async Task<IActionResult> LinkProject(int projectId, LinkPlannerProjectRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new LinkPlannerProjectCommand(projectId, request.ElementId, request.TemplateVersionId), cancellationToken));

    [HttpPost("nodes/tasks")]
    public async Task<IActionResult> CreateTaskNode(int projectId, CreatePlannerTaskNodeRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new CreatePlannerTaskNodeCommand(projectId, request.ElementId, request.Title,
            request.Description, request.StartDate, request.Priority, request.ExpectedCompletionDate,
            request.TemplateVersionId, request.ChangeReason), cancellationToken));

    [HttpPost("nodes/subtasks")]
    public async Task<IActionResult> CreateSubTaskNode(int projectId, CreatePlannerSubTaskNodeRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new CreatePlannerSubTaskNodeCommand(projectId, request.ElementId,
            request.TaskId, request.Title, request.TemplateVersionId, request.ChangeReason), cancellationToken));

    [HttpPut("nodes/{nodeId:guid}")]
    public async Task<IActionResult> UpdateNode(int projectId, Guid nodeId, UpdatePlannerNodeRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdatePlannerNodeCommand(projectId, nodeId, request.Title, request.Description,
            request.ExpectedCompletionDate, request.Priority, request.ProblemStatement, request.BudgetAmount,
            request.BudgetCurrency, request.ApproximateDurationWeeks, request.ChangeReason), cancellationToken);
        return NoContent();
    }

    [HttpDelete("nodes/{nodeId:guid}")]
    public async Task<IActionResult> RemoveNode(int projectId, Guid nodeId, [FromQuery] bool deleteEntity,
        [FromQuery] string? changeReason,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemovePlannerNodeCommand(projectId, nodeId, deleteEntity, changeReason), cancellationToken);
        return NoContent();
    }

    [HttpGet("resources")]
    public async Task<IActionResult> GetResources(int projectId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetPlannerResourcesQuery(projectId), cancellationToken));

    [HttpPost("resources/notes")]
    public async Task<IActionResult> CreateNote(int projectId, CreatePlannerNoteRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new CreatePlannerNoteCommand(projectId, request.ElementId,
            request.Title, request.Content, request.TemplateVersionId), cancellationToken));

    [HttpPost("resources/links")]
    public async Task<IActionResult> CreateLink(int projectId, CreatePlannerLinkRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new CreatePlannerLinkCommand(projectId, request.ElementId,
            request.Title, request.Url, request.TemplateVersionId), cancellationToken));

    [HttpPost("resources/documents")]
    [RequestSizeLimit(27 * 1024 * 1024)]
    [EnableRateLimiting("planner-upload")]
    public async Task<IActionResult> UploadDocument(int projectId,
        [FromForm] UploadPlannerDocumentRequest request, CancellationToken cancellationToken)
    {
        if (request.File is null)
            return BadRequest(new { code = "PLANNER_FILE_REQUIRED", message = "Choose a file to upload." });
        await using var stream = request.File.OpenReadStream();
        return Ok(await _mediator.Send(new UploadPlannerDocumentCommand(projectId,
            request.ElementId, request.Title, request.File.FileName,
            request.File.ContentType, request.File.Length, stream, request.TemplateVersionId), cancellationToken));
    }

    [HttpPost("resources/{resourceId:guid}/link")]
    public async Task<IActionResult> LinkResource(int projectId, Guid resourceId,
        LinkPlannerResourceRequest request, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new LinkPlannerResourceCommand(projectId, resourceId,
            request.ElementId, request.TemplateVersionId), cancellationToken));

    [HttpPut("resources/{resourceId:guid}")]
    public async Task<IActionResult> UpdateResource(int projectId, Guid resourceId,
        UpdatePlannerResourceRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdatePlannerResourceCommand(projectId, resourceId,
            request.Title, request.Content, request.Url, request.FileName), cancellationToken);
        return NoContent();
    }

    [HttpDelete("resources/{resourceId:guid}")]
    public async Task<IActionResult> DeleteResource(int projectId, Guid resourceId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeletePlannerResourceCommand(projectId, resourceId), cancellationToken);
        return NoContent();
    }

    [HttpGet("resources/{resourceId:guid}/content")]
    public async Task<IActionResult> GetResourceContent(int projectId, Guid resourceId,
        [FromQuery] bool download, CancellationToken cancellationToken)
    {
        var content = await _mediator.Send(
            new GetPlannerResourceContentQuery(projectId, resourceId), cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.Append("Cross-Origin-Resource-Policy", "same-origin");
        Response.Headers.Append("Content-Security-Policy", "sandbox; default-src 'none'");
        return download || !content.CanPreviewInline
            ? File(content.Content, content.ContentType, content.FileName)
            : File(content.Content, content.ContentType);
    }

    [HttpPost("requirements/finalize")]
    public async Task<IActionResult> FinalizeRequirements(int projectId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new FinalizePrimaryRequirementsCommand(projectId), cancellationToken));

    [HttpGet("requirements/baselines")]
    public async Task<IActionResult> GetBaselines(int projectId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetRequirementBaselinesQuery(projectId), cancellationToken));

    [HttpGet("requirements/baselines/{baselineId:guid}")]
    public async Task<IActionResult> GetBaseline(int projectId, Guid baselineId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetRequirementBaselineQuery(projectId, baselineId), cancellationToken));

    [HttpGet("requirements/changes")]
    public async Task<IActionResult> GetChanges(int projectId, [FromQuery] RequirementChangeType? changeType,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetRequirementChangesQuery(projectId, changeType), cancellationToken));

    [HttpGet("requirements/compare")]
    public async Task<IActionResult> CompareRequirements(int projectId, [FromQuery] Guid? baselineId,
        [FromQuery] RequirementChangeType? changeType, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new CompareRequirementsQuery(projectId, baselineId, changeType), cancellationToken));
}
