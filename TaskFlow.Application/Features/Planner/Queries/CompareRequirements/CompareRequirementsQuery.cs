using System.Text.Json;
using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Queries.CompareRequirements;

public sealed record CompareRequirementsQuery(int ProjectId, Guid? BaselineId, RequirementChangeType? ChangeType)
    : IRequest<RequirementComparisonDto?>;

public sealed class CompareRequirementsQueryHandler : IRequestHandler<CompareRequirementsQuery, RequirementComparisonDto?>
{
    private readonly IProjectRepository _projects; private readonly IRequirementBaselineRepository _baselines;
    private readonly ICurrentUserService _currentUser;
    public CompareRequirementsQueryHandler(IProjectRepository projects,
        IRequirementBaselineRepository baselines, ICurrentUserService currentUser)
    { _projects = projects; _baselines = baselines; _currentUser = currentUser; }

    public async Task<RequirementComparisonDto?> Handle(CompareRequirementsQuery request,
        CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        var baseline = request.BaselineId.HasValue
            ? await _baselines.GetByIdAsync(request.ProjectId, request.BaselineId.Value, cancellationToken)
            : await _baselines.GetLatestAsync(request.ProjectId, cancellationToken);
        if (baseline is null && request.BaselineId.HasValue)
            throw new NotFoundException("REQUIREMENT_BASELINE_NOT_FOUND", "Requirement baseline not found.");
        if (baseline is null) return null;

        var snapshots = baseline.Snapshots.ToDictionary(x => (x.EntityType, x.EntityId));
        var changes = await _baselines.GetChangesAsync(baseline.Id, cancellationToken);
        var latest = changes.GroupBy(x => (x.EntityType, x.EntityId))
            .Select(x => x.OrderByDescending(y => y.ChangedAt).First())
            .OrderByDescending(x => x.ChangedAt);
        var items = latest.Select(change =>
        {
            snapshots.TryGetValue((change.EntityType, change.EntityId), out var snapshot);
            var effectiveType = change.ChangeType == RequirementChangeType.Removed
                ? RequirementChangeType.Removed
                : snapshot is null ? RequirementChangeType.New : RequirementChangeType.Changed;
            var baselineJson = snapshot?.FieldsJson;
            return new RequirementComparisonItemDto(change.EntityType, change.EntityId, change.ParentEntityId,
                effectiveType, change.Title, change.ActorUserId, change.ChangedAt, change.Reason,
                Differences(baselineJson, change.NewValuesJson));
        }).Where(x => x.ChangeType != RequirementChangeType.Changed || x.Differences.Count > 0)
            .Where(x => !request.ChangeType.HasValue || x.ChangeType == request.ChangeType)
            .ToList();
        return new RequirementComparisonDto(baseline.Id, baseline.BaselineNumber, baseline.FinalizedAt, items);
    }

    private static IReadOnlyList<RequirementFieldDifferenceDto> Differences(string? beforeJson, string? afterJson)
    {
        var before = Parse(beforeJson); var after = Parse(afterJson);
        return before.Keys.Union(after.Keys).OrderBy(x => x).Where(key => before.GetValueOrDefault(key) != after.GetValueOrDefault(key))
            .Select(key => new RequirementFieldDifferenceDto(key, before.GetValueOrDefault(key), after.GetValueOrDefault(key))).ToList();
    }

    private static Dictionary<string, string?> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(x => x.Name,
            x => x.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : x.Value.ToString());
    }
}
