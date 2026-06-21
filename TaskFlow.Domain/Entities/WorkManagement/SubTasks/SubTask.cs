using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskStatus = TaskFlow.Domain.Enums.WorkManagement.TaskStatus;

namespace TaskFlow.Domain.Entities.WorkManagement.SubTasks;

public class SubTask : AuditableEntity
{
    public string Title { get; private set; }

    public TaskStatus Status { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public DateTime? CompletedDate { get; private set; }

    public int TaskId { get; private set; }

    protected SubTask()
    {
    }

    public SubTask(
        string title,
        int taskId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "SubTask title is required.");

        if (taskId <= 0)
            throw new ArgumentException(
                "TaskId is required.",
                nameof(taskId));

        Title = title.Trim();
        TaskId = taskId;

        Status = TaskStatus.Todo;
        CreatedDate = DateTime.UtcNow;
    }

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(
                "SubTask title is required.");

        Title = title.Trim();

        MarkAsUpdated();
    }

    public void Complete()
    {
        if (Status == TaskStatus.Completed)
            return;

        Status = TaskStatus.Completed;
        CompletedDate = DateTime.UtcNow;

        MarkAsUpdated();
    }

    public void Reopen()
    {
        Status = TaskStatus.Todo;
        CompletedDate = null;

        MarkAsUpdated();
    }
}