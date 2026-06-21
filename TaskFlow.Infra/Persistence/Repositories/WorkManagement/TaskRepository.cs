using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Infra.Persistence.Context;

using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Infra.Persistence.Repositories.WorkManagement
{
    public sealed class TaskRepository
        : ITaskRepository
    {
        private readonly TaskFlowDbContext _context;

        public TaskRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<TaskEntity?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Tasks
                .Include(x => x.SubTasks)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }
        public async Task<TaskEntity?> GetByTitleAsync(
    int organizationId,
    string title,
    CancellationToken cancellationToken = default)
        {
            var normalizedTitle =
                title.Trim().ToLower();

            return await _context.Tasks
                .FirstOrDefaultAsync(
                    x => x.OrganizationId == organizationId
                      && x.Title.ToLower() == normalizedTitle,
                    cancellationToken);
        }
        public async Task<IReadOnlyList<TaskEntity>>
            GetByOrganizationIdAsync(
                int organizationId,
                CancellationToken cancellationToken = default)
        {
            return await _context.Tasks
                .Where(x => x.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<TaskEntity>>
            GetByProjectIdAsync(
                int projectId,
                CancellationToken cancellationToken = default)
        {
            return await _context.Tasks
                .Where(x => x.ProjectId == projectId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<TaskEntity>>
            GetByCreatedByUserIdAsync(
                int userId,
                CancellationToken cancellationToken = default)
        {
            return await _context.Tasks
                .Where(x => x.CreatedByUserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Tasks
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task AddAsync(
            TaskEntity task,
            CancellationToken cancellationToken = default)
        {
            await _context.Tasks
                .AddAsync(
                    task,
                    cancellationToken);
        }

        public void Update(
            TaskEntity task)
        {
            _context.Tasks.Update(task);
        }

        public void Remove(
            TaskEntity task)
        {
            task.SoftDelete();

            _context.Tasks.Update(task);
        }
    }
}