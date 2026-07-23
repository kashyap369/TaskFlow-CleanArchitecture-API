namespace TaskFlow.Domain.DomainEvents.WorkManagement.Tasks
{
    public sealed class TaskAssignedEvent : DomainEvent
    {
        public int TaskId { get; }

        public int AssignedToUserId { get; }

        public int AssignedByUserId { get; }

        public int? PreviousAssignedToUserId { get; }

        public TaskAssignedEvent(
            int taskId,
            int assignedToUserId,
            int assignedByUserId,
            int? previousAssignedToUserId)
        {
            TaskId = taskId;
            AssignedToUserId = assignedToUserId;
            AssignedByUserId = assignedByUserId;
            PreviousAssignedToUserId = previousAssignedToUserId;
        }
    }
}
