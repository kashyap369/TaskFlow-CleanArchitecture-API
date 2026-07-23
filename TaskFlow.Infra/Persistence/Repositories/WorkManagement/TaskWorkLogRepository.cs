using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.WorkManagement.WorkLogs;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.WorkManagement
{
    public sealed class TaskWorkLogRepository
        : ITaskWorkLogRepository
    {
        private readonly TaskFlowDbContext _context;

        public TaskWorkLogRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<TaskWorkLog?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.TaskWorkLogs
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<TaskWorkLog?> GetRunningByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.TaskWorkLogs
                .FirstOrDefaultAsync(
                    x => x.UserId == userId
                      && x.EndedAt == null,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<TaskWorkLog>>
            GetByTaskIdAsync(
                int taskId,
                CancellationToken cancellationToken = default)
        {
            return await _context.TaskWorkLogs
                .Where(x => x.TaskId == taskId)
                .OrderByDescending(x => x.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<TaskWorkLog>>
            GetByUserIdAsync(
                int userId,
                DateTime from,
                DateTime to,
                CancellationToken cancellationToken = default)
        {
            return await _context.TaskWorkLogs
                .Where(x => x.UserId == userId
                         && x.StartedAt >= from
                         && x.StartedAt <= to)
                .OrderByDescending(x => x.StartedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            TaskWorkLog workLog,
            CancellationToken cancellationToken = default)
        {
            await _context.TaskWorkLogs
                .AddAsync(
                    workLog,
                    cancellationToken);
        }

        public void Update(
            TaskWorkLog workLog)
        {
            _context.TaskWorkLogs.Update(workLog);
        }

        public void Remove(
            TaskWorkLog workLog)
        {
            workLog.SoftDelete();

            _context.TaskWorkLogs.Update(workLog);
        }
    }
}
