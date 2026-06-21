using TaskFlow.Domain.Common;
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
    public int OrganizationId { get; private set; }

    public int CreatedByUserId { get; private set; }

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
     int organizationId,
     int createdByUserId,
     DateTime? expectedCompletionDate = null,
     int? projectId = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "Task title is required.");

        if (organizationId <= 0)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

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
    }

    public void RecalculateStatus()
    {
        if (!_subTasks.Any())
            return;

        if (_subTasks.All(x => x.Status == TaskStatus.Completed))
        {
            Status =TaskStatus.Completed;
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