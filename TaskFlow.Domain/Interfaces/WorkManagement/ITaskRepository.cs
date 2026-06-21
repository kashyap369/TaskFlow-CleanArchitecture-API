using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Domain.Interfaces.WorkManagement
{
    public interface ITaskRepository
    {
        Task<TaskEntity?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TaskEntity>> GetByOrganizationIdAsync(
            int organizationId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TaskEntity>> GetByProjectIdAsync(
            int projectId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TaskEntity>> GetByCreatedByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            TaskEntity task,
            CancellationToken cancellationToken = default);

        void Update(
            TaskEntity task);

        void Remove(
            TaskEntity task);
        Task<TaskEntity?> GetByTitleAsync(
    int organizationId,
    string title,
    CancellationToken cancellationToken = default);
    }
}