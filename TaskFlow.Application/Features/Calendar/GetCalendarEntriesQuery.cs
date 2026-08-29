using Dapper;
using FluentValidation;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Application.Features.Calendar;

public sealed record GetCalendarEntriesQuery(int OrganizationId, DateTimeOffset FromUtc, DateTimeOffset ToUtc)
    : IRequest<IReadOnlyList<CalendarEntryDto>>, IOrganizationScopedRequest;

public sealed class GetCalendarEntriesQueryValidator : AbstractValidator<GetCalendarEntriesQuery>
{
    public GetCalendarEntriesQueryValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x).Must(x => x.ToUtc > x.FromUtc).WithMessage("To must be after from.");
        RuleFor(x => x).Must(x => x.ToUtc - x.FromUtc <= TimeSpan.FromDays(366)).WithMessage("Calendar windows cannot exceed 366 days.");
    }
}

public sealed class GetCalendarEntriesQueryHandler : IRequestHandler<GetCalendarEntriesQuery, IReadOnlyList<CalendarEntryDto>>
{
    private readonly ISqlConnectionFactory _sql;
    public GetCalendarEntriesQueryHandler(ISqlConnectionFactory sql) => _sql = sql;
    public async Task<IReadOnlyList<CalendarEntryDto>> Handle(GetCalendarEntriesQuery request, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT e."Id", e."OrganizationId", e."Kind", e."Title", e."Description",
                   e."StartsAtUtc", e."EndsAtUtc", e."IsAllDay", e."TimeZone", e."MemberUserId",
                   CASE WHEN u."Id" IS NULL THEN NULL ELSE u."FirstName" || ' ' || u."LastName" END AS "MemberName",
                   e."RecurrenceFrequency", e."RecurrenceInterval", e."RecurrenceUntil"
            FROM "CalendarEntries" e
            LEFT JOIN "Users" u ON u."Id" = e."MemberUserId"
            WHERE e."OrganizationId" = @OrganizationId AND e."IsDeleted" = FALSE
              AND e."StartsAtUtc" < @ToUtc
              AND (e."RecurrenceFrequency" <> 0 OR e."EndsAtUtc" > @FromUtc)
              AND (e."RecurrenceUntil" IS NULL OR e."RecurrenceUntil" >= @FromDate::date)
            ORDER BY e."StartsAtUtc", e."Id";
            """;
        using var connection = _sql.Create();
        var rows = await connection.QueryAsync<CalendarEntryRow>(new CommandDefinition(sql, new
        {
            request.OrganizationId,
            FromUtc = request.FromUtc.UtcDateTime,
            ToUtc = request.ToUtc.UtcDateTime,
            FromDate = request.FromUtc.UtcDateTime.Date
        }, cancellationToken: cancellationToken));
        return rows.SelectMany(row => Expand(row, request.FromUtc.UtcDateTime, request.ToUtc.UtcDateTime))
            .OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id).ToList();
    }

    private static IEnumerable<CalendarEntryDto> Expand(CalendarEntryRow row, DateTime fromUtc, DateTime toUtc)
    {
        var start = DateTime.SpecifyKind(row.StartsAtUtc, DateTimeKind.Utc);
        var duration = row.EndsAtUtc - row.StartsAtUtc;
        var count = 0;
        while (count++ < 1000)
        {
            var end = start + duration;
            if (end > fromUtc && start < toUtc)
                yield return new(row.Id, $"{row.Id}:{start:O}", row.OrganizationId, row.Kind, row.Title,
                    row.Description, start, end, row.IsAllDay, row.TimeZone, row.MemberUserId,
                    row.MemberName, row.RecurrenceFrequency, row.RecurrenceInterval, row.RecurrenceUntil);
            if (row.RecurrenceFrequency == CalendarRecurrenceFrequency.None) yield break;
            start = row.RecurrenceFrequency switch
            {
                CalendarRecurrenceFrequency.Daily => start.AddDays(row.RecurrenceInterval),
                CalendarRecurrenceFrequency.Weekly => start.AddDays(7 * row.RecurrenceInterval),
                CalendarRecurrenceFrequency.Monthly => start.AddMonths(row.RecurrenceInterval),
                _ => throw new InvalidOperationException("Unsupported calendar recurrence frequency.")
            };
            if (start >= toUtc || row.RecurrenceUntil is DateOnly until && DateOnly.FromDateTime(start) > until)
                yield break;
        }
    }

    public sealed class CalendarEntryRow
    {
        public int Id { get; init; } public int OrganizationId { get; init; }
        public CalendarEntryKind Kind { get; init; } public string Title { get; init; } = string.Empty;
        public string? Description { get; init; } public DateTime StartsAtUtc { get; init; }
        public DateTime EndsAtUtc { get; init; } public bool IsAllDay { get; init; }
        public string TimeZone { get; init; } = "UTC"; public int? MemberUserId { get; init; }
        public string? MemberName { get; init; } public CalendarRecurrenceFrequency RecurrenceFrequency { get; init; }
        public int RecurrenceInterval { get; init; } public DateOnly? RecurrenceUntil { get; init; }
    }
}
