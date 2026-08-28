using TaskFlow.Domain.Enums.Planner;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;
using SubTaskEntity = TaskFlow.Domain.Entities.WorkManagement.SubTasks.SubTask;
using ProjectEntity = TaskFlow.Domain.Entities.WorkManagement.Projects.Project;

namespace TaskFlow.Domain.Entities.Planner;

public sealed class PlannerNode
{
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public string ElementId { get; private set; } = string.Empty;
    public PlannerNodeType NodeType { get; private set; }
    public int? ProjectId { get; private set; }
    public int? TaskId { get; private set; }
    public int? SubTaskId { get; private set; }
    public Guid? ResourceId { get; private set; }
    public Guid? TemplateVersionId { get; private set; }
    public PlannerTemplateVersion? TemplateVersion { get; private set; }
    public ProjectEntity? Project { get; private set; }
    public TaskEntity? Task { get; private set; }
    public SubTaskEntity? SubTask { get; private set; }
    public PlannerResource? Resource { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private PlannerNode()
    {
    }

    private PlannerNode(Guid boardId, string elementId, PlannerNodeType nodeType)
    {
        if (boardId == Guid.Empty)
            throw new ArgumentException("Board id is required.", nameof(boardId));
        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element id is required.", nameof(elementId));

        Id = Guid.NewGuid();
        BoardId = boardId;
        ElementId = elementId.Trim();
        NodeType = nodeType;
        CreatedAt = DateTime.UtcNow;
    }

    public static PlannerNode ForProject(Guid boardId, string elementId, ProjectEntity project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var node = new PlannerNode(boardId, elementId, PlannerNodeType.Project)
        {
            Project = project,
            ProjectId = project.Id,
        };
        return node;
    }

    public void ApplyTemplate(PlannerTemplateVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.ObjectType != NodeType) throw new InvalidOperationException("Template type does not match the Planner node type.");
        TemplateVersion = version;
        TemplateVersionId = version.Id;
        UpdatedAt = DateTime.UtcNow;
    }

    public static PlannerNode ForTask(Guid boardId, string elementId, TaskEntity task)
    {
        ArgumentNullException.ThrowIfNull(task);
        var node = new PlannerNode(boardId, elementId, PlannerNodeType.Task)
        {
            Task = task,
            TaskId = task.Id,
        };
        return node;
    }

    public static PlannerNode ForSubTask(Guid boardId, string elementId, SubTaskEntity subTask)
    {
        ArgumentNullException.ThrowIfNull(subTask);
        var node = new PlannerNode(boardId, elementId, PlannerNodeType.SubTask)
        {
            SubTask = subTask,
            SubTaskId = subTask.Id,
        };
        return node;
    }

    public static PlannerNode ForResource(Guid boardId, string elementId, PlannerResource resource,
        PlannerNodeType nodeType)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (nodeType is not (PlannerNodeType.Note or PlannerNodeType.Document))
            throw new ArgumentException("Resource nodes must be Note or Document.", nameof(nodeType));
        var node = new PlannerNode(boardId, elementId, nodeType)
        {
            Resource = resource,
            ResourceId = resource.Id,
        };
        return node;
    }
}
