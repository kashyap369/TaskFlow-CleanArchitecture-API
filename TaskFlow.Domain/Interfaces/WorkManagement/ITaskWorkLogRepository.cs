using TaskFlow.Domain.Entities.WorkManagement.WorkLogs;

namespace TaskFlow.Domain.Interfaces.WorkManagement
{
    public interface ITaskWorkLogRepository
    {
        Task<TaskWorkLog?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The user's currently running session, if any —
        /// used to enforce one running timer per user.
        /// </summary>
        Task<TaskWorkLog?> GetRunningByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TaskWorkLog>> GetByTaskIdAsync(
            int taskId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TaskWorkLog>> GetByUserIdAsync(
            int userId,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            TaskWorkLog workLog,
            CancellationToken cancellationToken = default);

        void Update(
            TaskWorkLog workLog);

        void Remove(
            TaskWorkLog workLog);
    }
}
