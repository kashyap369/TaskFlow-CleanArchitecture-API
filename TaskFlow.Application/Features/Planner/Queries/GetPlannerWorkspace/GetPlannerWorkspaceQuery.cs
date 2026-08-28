using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskStatus = TaskFlow.Domain.Enums.WorkManagement.TaskStatus;

namespace TaskFlow.Application.Features.Planner.Queries.GetPlannerWorkspace;

public sealed record GetPlannerWorkspaceQuery(int ProjectId) : IRequest<PlannerWorkspaceDto>;

public sealed class GetPlannerWorkspaceQueryHandler : IRequestHandler<GetPlannerWorkspaceQuery, PlannerWorkspaceDto>
{
    private readonly IProjectRepository _projects;
    private readonly IPlannerBoardRepository _boards;
    private readonly TaskFlow.Application.Contracts.Security.ICurrentUserService _currentUser;

    public GetPlannerWorkspaceQueryHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        TaskFlow.Application.Contracts.Security.ICurrentUserService currentUser)
    {
        _projects = projects;
        _boards = boards;
        _currentUser = currentUser;
    }

    public async Task<PlannerWorkspaceDto> Handle(GetPlannerWorkspaceQuery request, CancellationToken cancellationToken)
    {
        var project = await PersonalPlannerAccess.GetOwnedProjectAsync(
            request.ProjectId, _projects, _currentUser, cancellationToken);
        var board = await _boards.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");

        var tasks = project.Tasks.ToDictionary(x => x.Id);
        var subTasks = project.Tasks.SelectMany(x => x.SubTasks).ToDictionary(x => x.Id);
        var nodes = new List<PlannerNodeDto>();

        foreach (var node in board.Nodes.OrderBy(x => x.CreatedAt))
        {
            if (node.NodeType == PlannerNodeType.Project && node.ProjectId == project.Id)
            {
                nodes.Add(ToProjectNode(node, project));
            }
            else if (node.NodeType == PlannerNodeType.Task && node.TaskId is int taskId && tasks.TryGetValue(taskId, out var task))
            {
                nodes.Add(new PlannerNodeDto(node.Id, node.ElementId, node.NodeType, task.Id, project.Id,
                    task.Title, task.Description, (int)task.Status, (int)task.Priority, task.StartDate,
                    task.ExpectedCompletionDate, task.ActualCompletionDate, task.SubTasks.Count,
                    task.SubTasks.Count(x => x.Status == TaskStatus.Completed), task.GetCompletionPercentage(),
                    TemplateVersion: ToTemplateVersion(node.TemplateVersion)));
            }
            else if (node.NodeType == PlannerNodeType.SubTask && node.SubTaskId is int subTaskId && subTasks.TryGetValue(subTaskId, out var subTask))
            {
                nodes.Add(new PlannerNodeDto(node.Id, node.ElementId, node.NodeType, subTask.Id, subTask.TaskId,
                    subTask.Title, null, (int)subTask.Status, null, subTask.CreatedDate, null,
                    subTask.CompletedDate, 0, 0, subTask.Status == TaskStatus.Completed ? 100 : 0,
                    TemplateVersion: ToTemplateVersion(node.TemplateVersion)));
            }
            else if (node.NodeType is PlannerNodeType.Note or PlannerNodeType.Document && node.Resource is not null)
            {
                var resource = node.Resource;
                nodes.Add(new PlannerNodeDto(node.Id, node.ElementId, node.NodeType, null, project.Id,
                    resource.Title, resource.Content, 0, null, resource.CreatedAt, null, null, 0, 0, 0,
                    TemplateVersion: ToTemplateVersion(node.TemplateVersion), ResourceId: resource.Id,
                    ResourceKind: resource.Kind, ResourceUrl: resource.Url,
                    Asset: resource.Asset is null ? null : new PlannerAssetDto(resource.Asset.Id,
                        resource.Asset.FileName, resource.Asset.ContentType, resource.Asset.Size,
                        resource.Asset.Sha256, resource.Asset.ScanStatus, resource.Asset.CreatedAt)));
            }
        }

        var completedTasks = project.Tasks.Count(x => x.Status == TaskStatus.Completed);
        var allSubTasks = project.Tasks.SelectMany(x => x.SubTasks).ToList();
        var summary = new PlannerProjectSummaryDto(project.Title, project.Description, project.ProblemStatement,
            project.BudgetAmount, project.BudgetCurrency, project.ApproximateDurationWeeks, (int)project.Status,
            project.StartDate, project.ExpectedCompletionDate, project.ActualCompletionDate, project.Tasks.Count,
            completedTasks, allSubTasks.Count, allSubTasks.Count(x => x.Status == TaskStatus.Completed),
            project.GetCompletionPercentage());

        return new PlannerWorkspaceDto(board.Id, project.Id, summary, nodes);
    }

    private static PlannerNodeDto ToProjectNode(TaskFlow.Domain.Entities.Planner.PlannerNode node,
        TaskFlow.Domain.Entities.WorkManagement.Projects.Project project)
    {
        var completedTasks = project.Tasks.Count(x => x.Status == TaskStatus.Completed);
        return new PlannerNodeDto(node.Id, node.ElementId, PlannerNodeType.Project, project.Id, null, project.Title,
            project.Description, (int)project.Status, null, project.StartDate, project.ExpectedCompletionDate,
            project.ActualCompletionDate, project.Tasks.Count, completedTasks, project.GetCompletionPercentage(),
            project.ProblemStatement, project.BudgetAmount, project.BudgetCurrency, project.ApproximateDurationWeeks,
            ToTemplateVersion(node.TemplateVersion));
    }

    private static PlannerTemplateVersionDto? ToTemplateVersion(TaskFlow.Domain.Entities.Planner.PlannerTemplateVersion? v) =>
        v is null ? null : new PlannerTemplateVersionDto(v.Id, v.VersionNumber, v.ObjectType, v.Name, v.Icon,
            v.Header, v.BackgroundColor, v.StrokeColor, v.DefaultWidth, v.DefaultHeight, v.VisibleFieldsJson,
            v.DefaultValuesJson, v.PublishedByUserId, v.PublishedAt);
}
