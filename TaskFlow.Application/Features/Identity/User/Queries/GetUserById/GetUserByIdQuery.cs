using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Identity.User.DTOs.Queries;

namespace TaskFlow.Application.Features.Identity.User.Queries.GetUserById
{
    public sealed record GetUserByIdQuery(
        int UserId
    ) : IRequest<UserDetailDto>, IUserScopedRequest;

    public sealed class GetUserByIdQueryHandler
        : IRequestHandler<GetUserByIdQuery, UserDetailDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetUserByIdQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<UserDetailDto> Handle(
            GetUserByIdQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    "Id"                              AS "Id",
                    "FirstName"                       AS "FirstName",
                    "LastName"                        AS "LastName",
                    "FirstName" || ' ' || "LastName"  AS "FullName",
                    "Email"                           AS "Email",
                    "PhoneNumber"                     AS "PhoneNumber",
                    "Status"                          AS "Status",
                    "AccountType"                     AS "AccountType",
                    "IsEmailVerified"                 AS "IsEmailVerified",
                    "LastLoginAt"                     AS "LastLoginAt",
                    "CreatedAt"                       AS "CreatedAt"
                FROM "Users"
                WHERE "Id" = @UserId
                  AND "IsDeleted" = FALSE;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var user =
                await connection.QuerySingleOrDefaultAsync<UserDetailDto>(
                    new CommandDefinition(
                        sql,
                        new { request.UserId },
                        cancellationToken: cancellationToken));

            if (user is null)
            {
                throw new NotFoundException(
                    "USER_NOT_FOUND",
                    "User not found.");
            }

            return user;
        }
    }
}
