namespace TaskFlow.Application.Features.Planner.DTOs;

public sealed record PlannerBoardDto(
    Guid BoardId,
    int ProjectId,
    int Revision,
    string SceneJson,
    DateTime UpdatedAt,
    DateTime? LastOpenedAt);

public sealed record PlannerSceneRevisionListItemDto(
    int Revision,
    DateTime CreatedAt,
    int CreatedByUserId);

public sealed record PlannerSceneRevisionDto(
    Guid BoardId,
    int ProjectId,
    int Revision,
    string SceneJson,
    DateTime CreatedAt,
    int CreatedByUserId);
