using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Identity.User.DTOs.Queries;

namespace TaskFlow.Application.Features.Identity.User.Queries.GetUsers
{
    public sealed record GetUsersQuery
        : IRequest<IReadOnlyList<UserListItemDto>>;

    public sealed class GetUsersQueryHandler
        : IRequestHandler<GetUsersQuery, IReadOnlyList<UserListItemDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetUsersQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<UserListItemDto>> Handle(
            GetUsersQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    "Id"                              AS "Id",
                    "FirstName" || ' ' || "LastName"  AS "FullName",
                    "Email"                           AS "Email",
                    "Status"                          AS "Status",
                    "AccountType"                     AS "AccountType"
                FROM "Users"
                WHERE "IsDeleted" = FALSE
                ORDER BY "CreatedAt" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var users =
                await connection.QueryAsync<UserListItemDto>(
                    new CommandDefinition(
                        sql,
                        cancellationToken: cancellationToken));

            return users.ToList();
        }
    }
}
