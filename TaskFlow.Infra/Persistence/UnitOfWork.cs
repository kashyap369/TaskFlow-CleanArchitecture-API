using TaskFlow.Domain.Interfaces;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly TaskFlowDbContext _context;

        public UnitOfWork(TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(
                cancellationToken);
        }
    }
}