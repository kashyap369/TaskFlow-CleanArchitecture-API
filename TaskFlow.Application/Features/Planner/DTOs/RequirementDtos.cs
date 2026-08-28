using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Application.Features.Planner.DTOs;

public sealed record RequirementBaselineListItemDto(
    Guid Id,
    int BaselineNumber,
    int SnapshotCount,
    int FinalizedByUserId,
    DateTime FinalizedAt);

public sealed record RequirementBaselineDto(
    Guid Id,
    int ProjectId,
    int BaselineNumber,
    int FinalizedByUserId,
    DateTime FinalizedAt,
    IReadOnlyList<RequirementSnapshotDto> Snapshots);

public sealed record RequirementSnapshotDto(
    Guid Id,
    RequirementEntityType EntityType,
    int EntityId,
    int? ParentEntityId,
    int OrderIndex,
    string Title,
    string FieldsJson,
    DateTime CapturedAt);

public sealed record RequirementChangeDto(
    Guid Id,
    RequirementEntityType EntityType,
    int EntityId,
    int? ParentEntityId,
    RequirementChangeType ChangeType,
    string Title,
    string? OldValuesJson,
    string? NewValuesJson,
    int ActorUserId,
    DateTime ChangedAt,
    string? Reason);

public sealed record RequirementFieldDifferenceDto(
    string Field,
    string? BaselineValue,
    string? CurrentValue);

public sealed record RequirementComparisonItemDto(
    RequirementEntityType EntityType,
    int EntityId,
    int? ParentEntityId,
    RequirementChangeType ChangeType,
    string Title,
    int ActorUserId,
    DateTime ChangedAt,
    string? Reason,
    IReadOnlyList<RequirementFieldDifferenceDto> Differences);

public sealed record RequirementComparisonDto(
    Guid BaselineId,
    int BaselineNumber,
    DateTime FinalizedAt,
    IReadOnlyList<RequirementComparisonItemDto> Items);
