using TaskFlow.Domain.Entities.WorkManagement.SubTasks;

namespace TaskFlow.Domain.Interfaces.WorkManagement
{
    public interface ISubTaskRepository
    {
        Task<SubTask?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SubTask>> GetByTaskIdAsync(
            int taskId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            SubTask subTask,
            CancellationToken cancellationToken = default);

        void Update(
            SubTask subTask);

        void Remove(
            SubTask subTask);
        Task<SubTask?> GetByTitleAsync(
    int taskId,
    string title,
    CancellationToken cancellationToken = default);
    }
}