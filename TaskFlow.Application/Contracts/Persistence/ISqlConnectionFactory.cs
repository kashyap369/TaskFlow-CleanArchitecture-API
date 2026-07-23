using System.Data;

namespace TaskFlow.Application.Contracts.Persistence
{
    /// <summary>
    /// Creates open-able ADO.NET connections for the read side.
    /// Query handlers use these with Dapper to run raw SQL and
    /// map straight to DTOs, bypassing EF Core. Callers own the
    /// connection lifetime (wrap in <c>using</c>).
    /// </summary>
    public interface ISqlConnectionFactory
    {
        IDbConnection Create();
    }
}
