using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Application.Features.Planner.DTOs;

public sealed record PlannerWorkspaceDto(
    Guid BoardId,
    int ProjectId,
    PlannerProjectSummaryDto Project,
    IReadOnlyList<PlannerNodeDto> Nodes);

public sealed record PlannerProjectSummaryDto(
    string Title,
    string? Description,
    string? ProblemStatement,
    decimal? BudgetAmount,
    string? BudgetCurrency,
    int? ApproximateDurationWeeks,
    int Status,
    DateTime StartDate,
    DateTime? ExpectedCompletionDate,
    DateTime? ActualCompletionDate,
    int TotalTaskCount,
    int CompletedTaskCount,
    int TotalSubTaskCount,
    int CompletedSubTaskCount,
    decimal CompletionPercentage);

public sealed record PlannerNodeDto(
    Guid NodeId,
    string ElementId,
    PlannerNodeType NodeType,
    int? EntityId,
    int? ParentEntityId,
    string Title,
    string? Description,
    int Status,
    int? Priority,
    DateTime? StartDate,
    DateTime? ExpectedCompletionDate,
    DateTime? ActualCompletionDate,
    int ChildCount,
    int CompletedChildCount,
    decimal CompletionPercentage,
    string? ProblemStatement = null,
    decimal? BudgetAmount = null,
    string? BudgetCurrency = null,
    int? ApproximateDurationWeeks = null,
    PlannerTemplateVersionDto? TemplateVersion = null,
    Guid? ResourceId = null,
    PlannerResourceKind? ResourceKind = null,
    string? ResourceUrl = null,
    PlannerAssetDto? Asset = null);
