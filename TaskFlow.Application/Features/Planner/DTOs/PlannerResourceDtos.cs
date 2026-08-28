using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Application.Features.Planner.DTOs;

public sealed record PlannerAssetDto(Guid Id, string FileName, string ContentType, long Size,
    string Sha256, PlannerAssetScanStatus ScanStatus, DateTime CreatedAt);

public sealed record PlannerResourceDto(Guid Id, Guid BoardId, int ProjectId, PlannerResourceKind Kind,
    string Title, string? Content, string? Url, Guid? NodeId, string? ElementId,
    PlannerAssetDto? Asset, DateTime CreatedAt, DateTime? UpdatedAt);

public sealed record PlannerResourceContentDto(byte[] Content, string ContentType, string FileName,
    bool CanPreviewInline);
