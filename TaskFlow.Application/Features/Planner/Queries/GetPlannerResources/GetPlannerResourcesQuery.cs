using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Queries.GetPlannerResources;

public sealed record GetPlannerResourcesQuery(int ProjectId) : IRequest<IReadOnlyList<PlannerResourceDto>>;
public sealed record GetPlannerResourceContentQuery(int ProjectId, Guid ResourceId) : IRequest<PlannerResourceContentDto>;

public sealed class GetPlannerResourcesQueryHandler : IRequestHandler<GetPlannerResourcesQuery, IReadOnlyList<PlannerResourceDto>>
{
    private readonly IProjectRepository _projects; private readonly IPlannerBoardRepository _boards;
    private readonly IPlannerResourceRepository _resources; private readonly ICurrentUserService _currentUser;
    public GetPlannerResourcesQueryHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        IPlannerResourceRepository resources, ICurrentUserService currentUser)
    { _projects = projects; _boards = boards; _resources = resources; _currentUser = currentUser; }

    public async Task<IReadOnlyList<PlannerResourceDto>> Handle(GetPlannerResourcesQuery request,
        CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        var board = await _boards.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");
        var resources = await _resources.ListAsync(board.Id, cancellationToken);
        return resources.Select(resource =>
        {
            var node = board.Nodes.FirstOrDefault(x => x.ResourceId == resource.Id);
            return ToDto(resource, node?.Id, node?.ElementId);
        }).ToList();
    }

    internal static PlannerResourceDto ToDto(TaskFlow.Domain.Entities.Planner.PlannerResource resource,
        Guid? nodeId, string? elementId) => new(resource.Id, resource.BoardId, resource.ProjectId,
        resource.Kind, resource.Title, resource.Content, resource.Url, nodeId, elementId,
        resource.Asset is null ? null : new PlannerAssetDto(resource.Asset.Id, resource.Asset.FileName,
            resource.Asset.ContentType, resource.Asset.Size, resource.Asset.Sha256,
            resource.Asset.ScanStatus, resource.Asset.CreatedAt), resource.CreatedAt, resource.UpdatedAt);
}

public sealed class GetPlannerResourceContentQueryHandler : IRequestHandler<GetPlannerResourceContentQuery, PlannerResourceContentDto>
{
    private readonly IProjectRepository _projects; private readonly IPlannerResourceRepository _resources;
    private readonly ICurrentUserService _currentUser; private readonly IObjectStorage _storage;
    public GetPlannerResourceContentQueryHandler(IProjectRepository projects, IPlannerResourceRepository resources,
        ICurrentUserService currentUser, IObjectStorage storage)
    { _projects = projects; _resources = resources; _currentUser = currentUser; _storage = storage; }

    public async Task<PlannerResourceContentDto> Handle(GetPlannerResourceContentQuery request,
        CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        var resource = await _resources.GetAsync(request.ResourceId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_RESOURCE_NOT_FOUND", "Planner resource not found.");
        if (resource.ProjectId != request.ProjectId || resource.OwnerUserId != _currentUser.UserId)
            throw new ForbiddenException("PLANNER_RESOURCE_ACCESS_DENIED", "This resource does not belong to you.");
        var asset = resource.Asset ?? throw new ConflictException("PLANNER_RESOURCE_HAS_NO_FILE", "This resource has no uploaded file.");
        if (asset.ScanStatus != PlannerAssetScanStatus.Clean)
            throw new ConflictException("PLANNER_FILE_NOT_AVAILABLE", "This file is not available until its security scan passes.");
        var stored = await _storage.DownloadAsync(asset.StorageKey, cancellationToken);
        return new PlannerResourceContentDto(stored.Content, asset.ContentType, asset.FileName,
            PlannerResourcePolicy.CanPreviewInline(asset.ContentType));
    }
}
