namespace TaskFlow.Domain.DomainEvents.WorkManagement.Tasks
{
    public sealed class TaskUnassignedEvent : DomainEvent
    {
        public int TaskId { get; }

        public int PreviousAssignedToUserId { get; }

        public int UnassignedByUserId { get; }

        public TaskUnassignedEvent(
            int taskId,
            int previousAssignedToUserId,
            int unassignedByUserId)
        {
            TaskId = taskId;
            PreviousAssignedToUserId = previousAssignedToUserId;
            UnassignedByUserId = unassignedByUserId;
        }
    }
}
