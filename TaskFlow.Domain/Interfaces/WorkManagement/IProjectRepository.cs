using TaskFlow.Domain.Entities.WorkManagement.Projects;

namespace TaskFlow.Domain.Interfaces.WorkManagement
{
    public interface IProjectRepository
    {
        Task<Project?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Project>> GetByOrganizationIdAsync(
            int organizationId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Project>> GetByCreatedByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            int organizationId,
            string title,
            CancellationToken cancellationToken = default);
        Task<Project?> GetByNameAsync(
    int organizationId,
    string title,
    CancellationToken cancellationToken = default);

        Task AddAsync(
            Project project,
            CancellationToken cancellationToken = default);

        void Update(
            Project project);

        void Remove(
            Project project);
    }
}