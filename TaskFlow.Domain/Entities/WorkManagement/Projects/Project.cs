using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.WorkManagement;
using Task = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;
using TaskStatus = TaskFlow.Domain.Enums.WorkManagement.TaskStatus;
namespace TaskFlow.Domain.Entities.WorkManagement.Projects;

public class Project : AuditableEntity, IAggregateRoot
{
    private readonly List<Task> _tasks = new();

    public string Title { get; private set; }

    public string Description { get; private set; }

    public ProjectStatus Status { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime? ExpectedCompletionDate { get; private set; }

    public DateTime? ActualCompletionDate { get; private set; }
    public int OrganizationId { get; private set; }

    public int CreatedByUserId { get; private set; }

    public IReadOnlyCollection<Task> Tasks =>
        _tasks.AsReadOnly();

    protected Project()
    {
    }

    public Project(
     string title,
     string description,
     DateTime startDate,
     int organizationId,
     int createdByUserId,
     DateTime? expectedCompletionDate = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Project title is required.");

        if (organizationId <= 0)
            throw new ArgumentException(
                "OrganizationId is required.",
                nameof(organizationId));

        if (createdByUserId <= 0)
            throw new ArgumentException(
                "CreatedByUserId is required.",
                nameof(createdByUserId));

        Title = title;
        Description = description;
        StartDate = startDate;
        ExpectedCompletionDate = expectedCompletionDate;
        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;

        Status = ProjectStatus.Draft;
    }

    public void Start()
    {
        Status = ProjectStatus.Active;

        MarkAsUpdated();
    }

    public void UpdateDetails(
        string title,
        string description,
        DateTime? expectedCompletionDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Project title is required.");

        Title = title;
        Description = description;
        ExpectedCompletionDate = expectedCompletionDate;

        MarkAsUpdated();
    }

    public void AddTask(Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        _tasks.Add(task);

        RecalculateStatus();

        MarkAsUpdated();
    }

    public void RemoveTask(int taskId)
    {
        var task =
            _tasks.FirstOrDefault(x => x.Id == taskId);

        if (task is null)
            return;

        _tasks.Remove(task);

        RecalculateStatus();

        MarkAsUpdated();
    }

    public void RecalculateStatus()
    {
        if (!_tasks.Any())
            return;

        if (_tasks.All(x => x.Status == TaskStatus.Completed))
        {
            Status = ProjectStatus.Completed;
            ActualCompletionDate = DateTime.UtcNow;
        }
        else
        {
            Status = ProjectStatus.Active;
            ActualCompletionDate = null;
        }

        MarkAsUpdated();
    }

    public decimal GetCompletionPercentage()
    {
        if (!_tasks.Any())
            return 0;

        var completedTasks =
            _tasks.Count(x =>
                x.Status == TaskStatus.Completed);

        return Math.Round(
            ((decimal)completedTasks / _tasks.Count) * 100,
            2);
    }
}