using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly TaskFlowDbContext _context;

    public UnitOfWork(TaskFlowDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "CONCURRENCY_CONFLICT",
                "This record changed in another request. Reload and try again.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_PlannerSceneRevisions_BoardId_RevisionNumber",
            })
        {
            throw new ConflictException(
                "PLANNER_REVISION_CONFLICT",
                "This Planner board changed in another tab or device. Reload it before saving.");
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_context.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
