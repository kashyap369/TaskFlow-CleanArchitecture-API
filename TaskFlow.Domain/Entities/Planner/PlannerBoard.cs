using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Domain.Entities.Planner;

public sealed class PlannerBoard
{
    private readonly List<PlannerNode> _nodes = new();
    private readonly List<PlannerSceneRevision> _sceneRevisions = new();

    public Guid Id { get; private set; }
    public int ProjectId { get; private set; }
    public int OwnerUserId { get; private set; }
    public string SceneJson { get; private set; } = PlannerSceneDocument.Empty;
    public int CurrentRevision { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? LastOpenedAt { get; private set; }
    public Project Project { get; private set; } = null!;
    public IReadOnlyCollection<PlannerNode> Nodes => _nodes.AsReadOnly();
    public IReadOnlyCollection<PlannerSceneRevision> SceneRevisions => _sceneRevisions.AsReadOnly();

    private PlannerBoard()
    {
    }

    private PlannerBoard(Project project, int ownerUserId)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.IsPersonal)
            throw new ArgumentException("Planner boards currently support personal projects only.");
        if (ownerUserId <= 0 || project.CreatedByUserId != ownerUserId)
            throw new ArgumentException("Planner board owner must own the personal project.");

        Id = Guid.NewGuid();
        Project = project;
        ProjectId = project.Id;
        OwnerUserId = ownerUserId;
        SceneJson = PlannerSceneDocument.Empty;
        CurrentRevision = 0;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public static PlannerBoard Create(Project project, int ownerUserId) =>
        new(project, ownerUserId);

    public PlannerSceneRevision SaveScene(string sceneJson, int expectedRevision, int actorUserId)
    {
        if (expectedRevision != CurrentRevision)
            throw new InvalidOperationException("Planner board revision is stale.");
        if (actorUserId != OwnerUserId)
            throw new InvalidOperationException("Only the Planner board owner can save it.");

        PlannerSceneDocument.EnsureValid(sceneJson);

        CurrentRevision++;
        SceneJson = sceneJson;
        UpdatedAt = DateTime.UtcNow;
        LastOpenedAt = UpdatedAt;

        var revision = new PlannerSceneRevision(
            Id,
            CurrentRevision,
            sceneJson,
            actorUserId);

        _sceneRevisions.Add(revision);
        return revision;
    }

    public PlannerNode LinkProject(string elementId, Project project)
    {
        EnsureElementAvailable(elementId);
        if (project.Id != ProjectId)
            throw new InvalidOperationException("A Planner board can only link its owning project.");
        if (_nodes.Any(x => x.ProjectId == project.Id))
            throw new InvalidOperationException("The project is already linked to this board.");

        var node = PlannerNode.ForProject(Id, elementId, project);
        _nodes.Add(node);
        UpdatedAt = DateTime.UtcNow;
        return node;
    }

    public PlannerNode LinkTask(string elementId, TaskFlow.Domain.Entities.WorkManagement.Tasks.Task task)
    {
        EnsureElementAvailable(elementId);
        if (task.ProjectId != ProjectId || !task.IsPersonal || task.CreatedByUserId != OwnerUserId)
            throw new InvalidOperationException("Task does not belong to this Planner project.");
        if (_nodes.Any(x => x.TaskId == task.Id && task.Id > 0))
            throw new InvalidOperationException("The task is already linked to this board.");

        var node = PlannerNode.ForTask(Id, elementId, task);
        _nodes.Add(node);
        UpdatedAt = DateTime.UtcNow;
        return node;
    }

    public PlannerNode LinkSubTask(string elementId, TaskFlow.Domain.Entities.WorkManagement.SubTasks.SubTask subTask,
        TaskFlow.Domain.Entities.WorkManagement.Tasks.Task parentTask)
    {
        EnsureElementAvailable(elementId);
        if (parentTask.ProjectId != ProjectId || subTask.TaskId != parentTask.Id)
            throw new InvalidOperationException("Subtask does not belong to this Planner project.");
        if (_nodes.Any(x => x.SubTaskId == subTask.Id && subTask.Id > 0))
            throw new InvalidOperationException("The subtask is already linked to this board.");

        var node = PlannerNode.ForSubTask(Id, elementId, subTask);
        _nodes.Add(node);
        UpdatedAt = DateTime.UtcNow;
        return node;
    }

    public PlannerNode LinkResource(string elementId, PlannerResource resource, PlannerNodeType nodeType)
    {
        EnsureElementAvailable(elementId);
        if (resource.BoardId != Id || resource.ProjectId != ProjectId || resource.OwnerUserId != OwnerUserId)
            throw new InvalidOperationException("Resource does not belong to this Planner board.");
        if (_nodes.Any(x => x.ResourceId == resource.Id))
            throw new InvalidOperationException("The resource is already linked to this board.");
        var node = PlannerNode.ForResource(Id, elementId, resource, nodeType);
        _nodes.Add(node);
        UpdatedAt = DateTime.UtcNow;
        return node;
    }

    public PlannerNode? FindNode(Guid nodeId) => _nodes.FirstOrDefault(x => x.Id == nodeId);

    public void UnlinkNode(Guid nodeId)
    {
        var node = FindNode(nodeId);
        if (node is null)
            return;
        _nodes.Remove(node);
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureElementAvailable(string elementId)
    {
        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element id is required.", nameof(elementId));
        if (_nodes.Any(x => x.ElementId == elementId.Trim()))
            throw new InvalidOperationException("The canvas element is already linked.");
    }
}
