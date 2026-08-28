using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Application.Features.Planner.DTOs;

public sealed record PlannerTemplateVersionDto(Guid Id, int VersionNumber, PlannerNodeType ObjectType,
    string Name, string Icon, string Header, string BackgroundColor, string StrokeColor,
    int DefaultWidth, int DefaultHeight, string VisibleFieldsJson, string DefaultValuesJson,
    int PublishedByUserId, DateTime PublishedAt);

public sealed record PlannerTemplateDto(Guid Id, string Name, PlannerNodeType ObjectType,
    PlannerTemplateStatus Status, bool IsActive, int SortOrder, string Icon, string Header,
    string BackgroundColor, string StrokeColor, int DefaultWidth, int DefaultHeight,
    string VisibleFieldsJson, string DefaultValuesJson, int? CurrentVersionNumber,
    DateTime CreatedAt, DateTime? UpdatedAt, IReadOnlyList<PlannerTemplateVersionDto> Versions);
