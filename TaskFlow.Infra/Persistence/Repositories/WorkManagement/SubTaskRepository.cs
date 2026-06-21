using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.WorkManagement.SubTasks;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.WorkManagement
{
    public sealed class SubTaskRepository
        : ISubTaskRepository
    {
        private readonly TaskFlowDbContext _context;

        public SubTaskRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<SubTask?> GetByTitleAsync(
    int taskId,
    string title,
    CancellationToken cancellationToken = default)
        {
            var normalizedTitle =
                title.Trim().ToLower();

            return await _context.SubTasks
                .FirstOrDefaultAsync(
                    x => x.TaskId == taskId
                      && x.Title.ToLower() == normalizedTitle,
                    cancellationToken);
        }


        public async Task<SubTask?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.SubTasks
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<SubTask>> GetByTaskIdAsync(
            int taskId,
            CancellationToken cancellationToken = default)
        {
            return await _context.SubTasks
                .Where(x => x.TaskId == taskId)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.SubTasks
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task AddAsync(
            SubTask subTask,
            CancellationToken cancellationToken = default)
        {
            await _context.SubTasks
                .AddAsync(
                    subTask,
                    cancellationToken);
        }

        public void Update(
            SubTask subTask)
        {
            _context.SubTasks.Update(subTask);
        }

        public void Remove(
            SubTask subTask)
        {
            subTask.SoftDelete();

            _context.SubTasks.Update(subTask);
        }
    }
}