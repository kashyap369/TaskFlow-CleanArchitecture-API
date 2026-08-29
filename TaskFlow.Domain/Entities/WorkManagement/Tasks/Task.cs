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

    /// <summary>
    /// Expected effort for capacity planning. Null means the task has not been
    /// estimated; zero is valid for a genuinely effort-free milestone.
    /// </summary>
    public int? EstimateMinutes { get; private set; }

    public int? ProjectId { get; private set; }

    /// <summary>
    /// Null for a personal task (Individual account).
    /// Set for an organization task.
    /// </summary>
    public int? OrganizationId { get; private set; }

    public int CreatedByUserId { get; private set; }

    /// <summary>
    /// The team responsible for this task, so tasks (not just
    /// reports) can be viewed per team. Optional: a task may
    /// belong to an organization without belonging to a team.
    /// Always null for a personal task — teams only exist inside
    /// organizations.
    /// </summary>
    public int? TeamId { get; private set; }

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
     int? projectId = null,
     int? teamId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "Task title is required.");

        if (organizationId.HasValue && organizationId <= 0)
            throw new ArgumentException(
                "OrganizationId must be positive.",
                nameof(organizationId));

        // Teams are an organization concept. Projects may now be either
        // organization-scoped or private to an Individual account; the command
        // handler validates that the task and project have the same scope.
        if (teamId.HasValue && !organizationId.HasValue)
            throw new ArgumentException(
                "A personal task cannot belong to a team.",
                nameof(teamId));

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
        TeamId = teamId;

        Status = TaskStatus.Todo;
    }

    /// <summary>
    /// Moves the task to a team, or clears the team when
    /// <paramref name="teamId"/> is null. Personal tasks cannot
    /// belong to a team. The caller is responsible for checking
    /// that the team is in the same organization — the entity
    /// has no way to know.
    /// </summary>
    public void AssignToTeam(int? teamId)
    {
        if (teamId.HasValue && IsPersonal)
            throw new InvalidOperationException(
                "Personal tasks cannot belong to a team.");

        if (teamId.HasValue && teamId <= 0)
            throw new ArgumentException(
                "TeamId must be positive.",
                nameof(teamId));

        if (TeamId == teamId)
            return;

        TeamId = teamId;

        MarkAsUpdated();
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

    /// <summary>
    /// Changes only the task's planning window. Calendar interactions use this
    /// focused operation instead of the general details update so a drag can
    /// never overwrite title, description, priority, team, or assignment data.
    /// </summary>
    public void Reschedule(
        DateTime startDate,
        DateTime? expectedCompletionDate)
    {
        if (startDate == default)
            throw new ArgumentException(
                "Task start date is required.",
                nameof(startDate));

        if (expectedCompletionDate.HasValue &&
            expectedCompletionDate.Value < startDate)
        {
            throw new ArgumentException(
                "Expected completion date cannot be before the start date.",
                nameof(expectedCompletionDate));
        }

        StartDate = startDate;
        ExpectedCompletionDate = expectedCompletionDate;

        MarkAsUpdated();
    }

    public void SetEstimate(int? estimateMinutes)
    {
        if (estimateMinutes < 0)
            throw new ArgumentOutOfRangeException(
                nameof(estimateMinutes),
                "Task estimate cannot be negative.");

        EstimateMinutes = estimateMinutes;

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
    /// Reopens a completed task, so the documented lifecycle
    /// (Todo → InProgress → Completed, reopen) is real for tasks
    /// and not only for subtasks. Mirrors
    /// <see cref="SubTask.Reopen"/>. A task that has subtasks has
    /// its status owned by them, so this defers to
    /// <see cref="RecalculateStatus"/> rather than guessing.
    /// </summary>
    public void Reopen()
    {
        if (Status != TaskStatus.Completed)
            return;

        ActualCompletionDate = null;

        if (_subTasks.Any())
        {
            RecalculateStatus();

            return;
        }

        Status = TaskStatus.Todo;

        MarkAsUpdated();
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
