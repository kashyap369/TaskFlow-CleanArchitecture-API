using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Planner.DTOs;

namespace TaskFlow.Application.Features.Planner.Queries.GetPlannerTemplates;

public sealed record GetPlannerTemplatesQuery(bool IncludeDrafts) : IRequest<IReadOnlyList<PlannerTemplateDto>>;

public sealed class GetPlannerTemplatesQueryHandler(ISqlConnectionFactory connections)
    : IRequestHandler<GetPlannerTemplatesQuery, IReadOnlyList<PlannerTemplateDto>>
{
    public async Task<IReadOnlyList<PlannerTemplateDto>> Handle(GetPlannerTemplatesQuery request, CancellationToken cancellationToken)
    {
        const string templatesSql = """
            SELECT "Id", "Name", "ObjectType", "Status", "IsActive", "SortOrder", "Icon", "Header",
                   "BackgroundColor", "StrokeColor", "DefaultWidth", "DefaultHeight", "VisibleFieldsJson",
                   "DefaultValuesJson", "CurrentVersionNumber", "CreatedAt", "UpdatedAt"
            FROM "PlannerTemplates"
            WHERE (@IncludeDrafts OR ("Status" = 2 AND "IsActive" = TRUE))
            ORDER BY "SortOrder", "Name";
            """;
        const string versionsSql = """
            SELECT "Id", "TemplateId", "VersionNumber", "ObjectType", "Name", "Icon", "Header",
                   "BackgroundColor", "StrokeColor", "DefaultWidth", "DefaultHeight", "VisibleFieldsJson",
                   "DefaultValuesJson", "PublishedByUserId", "PublishedAt"
            FROM "PlannerTemplateVersions"
            ORDER BY "TemplateId", "VersionNumber" DESC;
            """;
        using var connection = connections.Create();
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(
            templatesSql + versionsSql, new { request.IncludeDrafts }, cancellationToken: cancellationToken));
        var templates = (await multi.ReadAsync<TemplateRow>()).ToList();
        var versions = (await multi.ReadAsync<VersionRow>()).GroupBy(x => x.TemplateId).ToDictionary(x => x.Key, x => x.ToList());
        return templates.Select(x => new PlannerTemplateDto(x.Id, x.Name, (Domain.Enums.Planner.PlannerNodeType)x.ObjectType,
            (Domain.Enums.Planner.PlannerTemplateStatus)x.Status, x.IsActive, x.SortOrder, x.Icon, x.Header,
            x.BackgroundColor, x.StrokeColor, x.DefaultWidth, x.DefaultHeight, x.VisibleFieldsJson,
            x.DefaultValuesJson, x.CurrentVersionNumber, x.CreatedAt, x.UpdatedAt,
            versions.GetValueOrDefault(x.Id, [])
                .Where(v => request.IncludeDrafts || v.VersionNumber == x.CurrentVersionNumber)
                .Select(v => new PlannerTemplateVersionDto(v.Id, v.VersionNumber,
                (Domain.Enums.Planner.PlannerNodeType)v.ObjectType, v.Name, v.Icon, v.Header, v.BackgroundColor,
                v.StrokeColor, v.DefaultWidth, v.DefaultHeight, v.VisibleFieldsJson, v.DefaultValuesJson,
                v.PublishedByUserId, v.PublishedAt)).ToList())).ToList();
    }
    private sealed record TemplateRow(Guid Id, string Name, int ObjectType, int Status, bool IsActive, int SortOrder,
        string Icon, string Header, string BackgroundColor, string StrokeColor, int DefaultWidth, int DefaultHeight,
        string VisibleFieldsJson, string DefaultValuesJson, int? CurrentVersionNumber, DateTime CreatedAt, DateTime? UpdatedAt);
    private sealed record VersionRow(Guid Id, Guid TemplateId, int VersionNumber, int ObjectType, string Name,
        string Icon, string Header, string BackgroundColor, string StrokeColor, int DefaultWidth, int DefaultHeight,
        string VisibleFieldsJson, string DefaultValuesJson, int PublishedByUserId, DateTime PublishedAt);
}
