using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TaskFlow.Application.Contracts.Persistence;

namespace TaskFlow.Infra.Dapper
{
    public sealed class SqlConnectionFactory
        : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(
            IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection connection string is missing.");
        }

        public IDbConnection Create()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
