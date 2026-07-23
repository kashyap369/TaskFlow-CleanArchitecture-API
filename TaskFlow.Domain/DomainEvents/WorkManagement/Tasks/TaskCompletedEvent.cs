namespace TaskFlow.Domain.DomainEvents.WorkManagement.Tasks
{
    public sealed class TaskCompletedEvent : DomainEvent
    {
        public int TaskId { get; }

        public int? AssignedToUserId { get; }

        public TaskCompletedEvent(
            int taskId,
            int? assignedToUserId)
        {
            TaskId = taskId;
            AssignedToUserId = assignedToUserId;
        }
    }
}
