using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Application.Features.Planner;

internal static class RequirementDtoMapper
{
    public static RequirementBaselineDto ToDto(RequirementBaseline baseline) =>
        new(
            baseline.Id,
            baseline.ProjectId,
            baseline.BaselineNumber,
            baseline.FinalizedByUserId,
            baseline.FinalizedAt,
            baseline.Snapshots.OrderBy(x => x.OrderIndex).Select(x => new RequirementSnapshotDto(
                x.Id, x.EntityType, x.EntityId, x.ParentEntityId, x.OrderIndex, x.Title,
                x.FieldsJson, x.CapturedAt)).ToList());

    public static RequirementChangeDto ToDto(RequirementChange change) =>
        new(change.Id, change.EntityType, change.EntityId, change.ParentEntityId, change.ChangeType,
            change.Title, change.OldValuesJson, change.NewValuesJson, change.ActorUserId,
            change.ChangedAt, change.Reason);
}
