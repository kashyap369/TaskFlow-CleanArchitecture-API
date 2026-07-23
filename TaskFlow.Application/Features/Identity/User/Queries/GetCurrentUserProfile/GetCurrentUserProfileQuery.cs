using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Identity.User.DTOs.Queries;

namespace TaskFlow.Application.Features.Identity.User.Queries.GetCurrentUserProfile
{
    public sealed record GetCurrentUserProfileQuery
        : IRequest<UserDetailDto>;

    public sealed class GetCurrentUserProfileQueryHandler
        : IRequestHandler<GetCurrentUserProfileQuery, UserDetailDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICurrentUserService _currentUserService;

        public GetCurrentUserProfileQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICurrentUserService currentUserService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _currentUserService = currentUserService;
        }

        public async Task<UserDetailDto> Handle(
            GetCurrentUserProfileQuery request,
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
                        new { UserId = _currentUserService.UserId },
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
