using TaskFlow.Domain.Common;
using TaskFlow.Domain.DomainEvents.WorkManagement.Tasks;
using TaskFlow.Domain.Entities.WorkManagement.SubTasks;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskStatus = TaskFlow.Domain.Enums.WorkManagement.TaskStatus;

namespace TaskFlow.Domain.Entities.WorkManagement.Tasks;

public class Task : AuditableEntity
{
    private readonly List<SubTask> _subTasks = new();

    public string Title { get; private set; }

    public string Description { get; private set; }

    public TaskPriority Priority { get; private set; }

    public TaskStatus Status { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? ExpectedCompletionDate { get; private set; }

    public DateTime? ActualCompletionDate { get; private set; }

    public int? ProjectId { get; private set; }

    /// <summary>
    /// Null for a personal task (Individual account).
    /// Set for an organization task.
    /// </summary>
    public int? OrganizationId { get; private set; }

    public int CreatedByUserId { get; private set; }

    /// <summary>
    /// The member currently assigned to work on this task.
    /// Only organization tasks can be assigned.
    /// </summary>
    public int? AssignedToUserId { get; private set; }

    public bool IsPersonal =>
        !OrganizationId.HasValue;

    public IReadOnlyCollection<SubTask> SubTasks =>
        _subTasks.AsReadOnly();

    protected Task()
    {
    }

    public Task(
     string title,
     string description,
     DateTime startDate,
     TaskPriority priority,
     int? organizationId,
     int createdByUserId,
     DateTime? expectedCompletionDate = null,
     int? projectId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "Task title is required.");

        if (organizationId.HasValue && organizationId <= 0)
            throw new ArgumentException(
                "OrganizationId must be positive.",
                nameof(organizationId));

        // Projects only exist inside organizations, so a
        // personal task can never belong to a project.
        if (projectId.HasValue && !organizationId.HasValue)
            throw new ArgumentException(
                "A personal task cannot belong to a project.",
                nameof(projectId));

        if (createdByUserId <= 0)
            throw new ArgumentException(
                "CreatedByUserId is required.",
                nameof(createdByUserId));

        Title = title.Trim();
        Description = description?.Trim();
        StartDate = startDate;
        Priority = priority;
        ExpectedCompletionDate = expectedCompletionDate;
        ProjectId = projectId;
        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;

        Status = TaskStatus.Todo;
    }

    public void Start()
    {
        if (Status == TaskStatus.InProgress)
            return;

        if (Status == TaskStatus.Completed)
        {
            throw new InvalidOperationException(
                "Completed task cannot be started.");
        }

        Status = TaskStatus.InProgress;

        MarkAsUpdated();
    }

    public void UpdateDetails(
        string title,
        string description,
        TaskPriority priority,
        DateTime? expectedCompletionDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Task title is required.");

        Title = title;
        Description = description;
        Priority = priority;
        ExpectedCompletionDate = expectedCompletionDate;

        MarkAsUpdated();
    }

    public void AddSubTask(SubTask subTask)
    {
        ArgumentNullException.ThrowIfNull(subTask);

        _subTasks.Add(subTask);

        MarkAsUpdated();
    }

    public void RemoveSubTask(int subTaskId)
    {
        var subTask =
            _subTasks.FirstOrDefault(x => x.Id == subTaskId);

        if (subTask is null)
            return;

        _subTasks.Remove(subTask);

        RecalculateStatus();

        MarkAsUpdated();
    }

    public void Complete()
    {
        if (Status == TaskStatus.Completed)
            return;

        if (_subTasks.Any())
            throw new InvalidOperationException(
                "Task completion is controlled by SubTasks.");

        Status = TaskStatus.Completed;
        ActualCompletionDate = DateTime.UtcNow;

        MarkAsUpdated();

        AddDomainEvent(
            new TaskCompletedEvent(
                Id,
                AssignedToUserId));
    }

    /// <summary>
    /// Assigns the task to an organization member. Personal
    /// tasks cannot be assigned. Reassigning raises a new
    /// TaskAssignedEvent carrying the previous assignee, so
    /// assignment history is available for reports.
    /// </summary>
    public void Assign(
        int assignedToUserId,
        int assignedByUserId)
    {
        if (IsPersonal)
            throw new InvalidOperationException(
                "Personal tasks cannot be assigned.");

        if (assignedToUserId <= 0)
            throw new ArgumentException(
                "AssignedToUserId is required.",
                nameof(assignedToUserId));

        if (Status == TaskStatus.Completed)
            throw new InvalidOperationException(
                "Completed task cannot be assigned.");

        if (AssignedToUserId == assignedToUserId)
            return;

        var previousAssignedToUserId = AssignedToUserId;

        AssignedToUserId = assignedToUserId;

        MarkAsUpdated();

        AddDomainEvent(
            new TaskAssignedEvent(
                Id,
                assignedToUserId,
                assignedByUserId,
                previousAssignedToUserId));
    }

    public void Unassign(int unassignedByUserId)
    {
        if (!AssignedToUserId.HasValue)
            return;

        var previousAssignedToUserId = AssignedToUserId.Value;

        AssignedToUserId = null;

        MarkAsUpdated();

        AddDomainEvent(
            new TaskUnassignedEvent(
                Id,
                previousAssignedToUserId,
                unassignedByUserId));
    }

    public void RecalculateStatus()
    {
        if (!_subTasks.Any())
            return;

        if (_subTasks.All(x => x.Status == TaskStatus.Completed))
        {
            if (Status != TaskStatus.Completed)
            {
                AddDomainEvent(
                    new TaskCompletedEvent(
                        Id,
                        AssignedToUserId));
            }

            Status = TaskStatus.Completed;
            ActualCompletionDate = DateTime.UtcNow;
        }
        else
        {
            Status = TaskStatus.InProgress;
            ActualCompletionDate = null;
        }

        MarkAsUpdated();
    }

    public decimal GetCompletionPercentage()
    {
        if (!_subTasks.Any())
            return Status == TaskStatus.Completed
                ? 100
                : 0;

        var completed =
            _subTasks.Count(x =>
                x.Status == TaskStatus.Completed);

        return Math.Round(
            ((decimal)completed / _subTasks.Count) * 100,
            2);
    }
}