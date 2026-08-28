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

    public string? ProblemStatement { get; private set; }

    public decimal? BudgetAmount { get; private set; }

    public string? BudgetCurrency { get; private set; }

    public int? ApproximateDurationWeeks { get; private set; }
    /// <summary>
    /// Null for a private Individual project. Set for an organization project.
    /// </summary>
    public int? OrganizationId { get; private set; }

    public int CreatedByUserId { get; private set; }

    public bool IsPersonal => !OrganizationId.HasValue;

    public IReadOnlyCollection<Task> Tasks =>
        _tasks.AsReadOnly();

    protected Project()
    {
    }

    public Project(
     string title,
     string description,
     DateTime startDate,
     int? organizationId,
     int createdByUserId,
     DateTime? expectedCompletionDate = null,
     string? problemStatement = null,
     decimal? budgetAmount = null,
     string? budgetCurrency = null,
     int? approximateDurationWeeks = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Project title is required.");

        if (organizationId.HasValue && organizationId <= 0)
            throw new ArgumentException(
                "OrganizationId must be positive when supplied.",
                nameof(organizationId));

        if (createdByUserId <= 0)
            throw new ArgumentException(
                "CreatedByUserId is required.",
                nameof(createdByUserId));

        Title = title;
        Description = description;
        StartDate = startDate;
        ExpectedCompletionDate = expectedCompletionDate;
        SetPlanningDetails(problemStatement, budgetAmount, budgetCurrency, approximateDurationWeeks);
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
        DateTime? expectedCompletionDate,
        string? problemStatement = null,
        decimal? budgetAmount = null,
        string? budgetCurrency = null,
        int? approximateDurationWeeks = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Project title is required.");

        Title = title;
        Description = description;
        ExpectedCompletionDate = expectedCompletionDate;
        SetPlanningDetails(problemStatement, budgetAmount, budgetCurrency, approximateDurationWeeks);

        MarkAsUpdated();
    }

    private void SetPlanningDetails(
        string? problemStatement,
        decimal? budgetAmount,
        string? budgetCurrency,
        int? approximateDurationWeeks)
    {
        if (budgetAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(budgetAmount), "Budget cannot be negative.");
        if (approximateDurationWeeks is <= 0)
            throw new ArgumentOutOfRangeException(nameof(approximateDurationWeeks), "Duration must be positive.");
        if (budgetAmount.HasValue && string.IsNullOrWhiteSpace(budgetCurrency))
            throw new ArgumentException("Budget currency is required when a budget is supplied.", nameof(budgetCurrency));

        ProblemStatement = string.IsNullOrWhiteSpace(problemStatement) ? null : problemStatement.Trim();
        BudgetAmount = budgetAmount;
        BudgetCurrency = string.IsNullOrWhiteSpace(budgetCurrency) ? null : budgetCurrency.Trim().ToUpperInvariant();
        ApproximateDurationWeeks = approximateDurationWeeks;
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
