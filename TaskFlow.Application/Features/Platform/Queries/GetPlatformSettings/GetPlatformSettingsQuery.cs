using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Platform.DTOs.Queries;

namespace TaskFlow.Application.Features.Platform.Queries.GetPlatformSettings
{
    /// <summary>
    /// The platform settings singleton. No access-scope marker — this
    /// is platform data, not organization data; the route carries the
    /// <c>AdminOnly</c> policy.
    /// </summary>
    public sealed record GetPlatformSettingsQuery
        : IRequest<PlatformSettingDto>;

    public sealed class GetPlatformSettingsQueryHandler
        : IRequestHandler<GetPlatformSettingsQuery, PlatformSettingDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetPlatformSettingsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<PlatformSettingDto> Handle(
            GetPlatformSettingsQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    "Id"                 AS "Id",
                    "ApplicationName"    AS "ApplicationName",
                    "SupportEmail"       AS "SupportEmail",
                    "RegistrationOpen"   AS "RegistrationOpen",
                    "MaintenanceMode"    AS "MaintenanceMode",
                    "MaintenanceMessage" AS "MaintenanceMessage",
                    "CreatedAt"          AS "CreatedAt",
                    "UpdatedAt"          AS "UpdatedAt"
                FROM "PlatformSettings"
                WHERE "IsDeleted" = FALSE
                ORDER BY "Id"
                LIMIT 1;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var settings =
                await connection.QuerySingleOrDefaultAsync<PlatformSettingDto>(
                    new CommandDefinition(
                        sql,
                        cancellationToken: cancellationToken));

            if (settings is null)
            {
                // Only reachable if the seeder never ran — a
                // deployment fault, not an empty-state the client
                // should render.
                throw new NotFoundException(
                    "PLATFORM_SETTINGS_NOT_FOUND",
                    "Platform settings have not been initialised.");
            }

            return settings;
        }
    }
}
