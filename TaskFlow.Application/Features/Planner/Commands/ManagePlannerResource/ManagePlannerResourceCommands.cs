using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Commands.ManagePlannerResource;

public sealed record CreatePlannerNoteCommand(int ProjectId, string ElementId, string Title,
    string Content, Guid? TemplateVersionId = null) : IRequest<Guid>;
public sealed record CreatePlannerLinkCommand(int ProjectId, string ElementId, string Title,
    string Url, Guid? TemplateVersionId = null) : IRequest<Guid>;
public sealed record UploadPlannerDocumentCommand(int ProjectId, string ElementId, string Title,
    string FileName, string ContentType, long Length, Stream Content,
    Guid? TemplateVersionId = null) : IRequest<Guid>;
public sealed record LinkPlannerResourceCommand(int ProjectId, Guid ResourceId, string ElementId,
    Guid? TemplateVersionId = null) : IRequest<Guid>;
public sealed record UpdatePlannerResourceCommand(int ProjectId, Guid ResourceId, string Title,
    string? Content, string? Url, string? FileName) : IRequest;
public sealed record DeletePlannerResourceCommand(int ProjectId, Guid ResourceId) : IRequest;

internal sealed class PlannerResourceCommandContext
{
    public IProjectRepository Projects { get; }
    public IPlannerBoardRepository Boards { get; }
    public IPlannerResourceRepository Resources { get; }
    public IPlannerTemplateRepository Templates { get; }
    public ICurrentUserService CurrentUser { get; }
    public IUnitOfWork UnitOfWork { get; }

    public PlannerResourceCommandContext(IProjectRepository projects, IPlannerBoardRepository boards,
        IPlannerResourceRepository resources, IPlannerTemplateRepository templates,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    { Projects = projects; Boards = boards; Resources = resources; Templates = templates; CurrentUser = currentUser; UnitOfWork = unitOfWork; }

    public async Task<PlannerBoard> GetBoardAsync(int projectId, CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(projectId, Projects, CurrentUser, cancellationToken);
        return await Boards.GetByProjectIdAsync(projectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");
    }

    public async Task<PlannerResource> GetResourceAsync(int projectId, Guid resourceId,
        CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(projectId, Projects, CurrentUser, cancellationToken);
        var resource = await Resources.GetAsync(resourceId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_RESOURCE_NOT_FOUND", "Planner resource not found.");
        if (resource.ProjectId != projectId || resource.OwnerUserId != CurrentUser.UserId)
            throw new ForbiddenException("PLANNER_RESOURCE_ACCESS_DENIED", "This resource does not belong to you.");
        return resource;
    }

    public async Task<Guid> AddAsync(PlannerBoard board, PlannerResource resource, string elementId,
        PlannerNodeType nodeType, Guid? templateVersionId, CancellationToken cancellationToken)
    {
        if (board.Nodes.Any(x => x.ElementId == elementId))
            throw new ConflictException("PLANNER_ELEMENT_ALREADY_LINKED", "This canvas element is already linked.");
        await Resources.AddAsync(resource, cancellationToken);
        var node = board.LinkResource(elementId, resource, nodeType);
        var version = await PlannerTemplateAccess.ResolveAsync(templateVersionId, nodeType, Templates, cancellationToken);
        if (version is not null) node.ApplyTemplate(version);
        Boards.Update(board);
        await UnitOfWork.SaveChangesAsync(cancellationToken);
        return node.Id;
    }
}

public sealed class CreatePlannerNoteCommandHandler : IRequestHandler<CreatePlannerNoteCommand, Guid>
{
    private readonly PlannerResourceCommandContext _context;
    public CreatePlannerNoteCommandHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        IPlannerResourceRepository resources, IPlannerTemplateRepository templates,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork) =>
        _context = new(projects, boards, resources, templates, currentUser, unitOfWork);

    public async Task<Guid> Handle(CreatePlannerNoteCommand request, CancellationToken cancellationToken)
    {
        var board = await _context.GetBoardAsync(request.ProjectId, cancellationToken);
        var resource = PlannerResource.CreateNote(board.Id, board.ProjectId, board.OwnerUserId,
            request.Title, request.Content);
        return await _context.AddAsync(board, resource, request.ElementId, PlannerNodeType.Note,
            request.TemplateVersionId, cancellationToken);
    }
}

public sealed class CreatePlannerLinkCommandHandler : IRequestHandler<CreatePlannerLinkCommand, Guid>
{
    private readonly PlannerResourceCommandContext _context;
    public CreatePlannerLinkCommandHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        IPlannerResourceRepository resources, IPlannerTemplateRepository templates,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork) =>
        _context = new(projects, boards, resources, templates, currentUser, unitOfWork);

    public async Task<Guid> Handle(CreatePlannerLinkCommand request, CancellationToken cancellationToken)
    {
        var board = await _context.GetBoardAsync(request.ProjectId, cancellationToken);
        var resource = PlannerResource.CreateLink(board.Id, board.ProjectId, board.OwnerUserId,
            request.Title, request.Url);
        return await _context.AddAsync(board, resource, request.ElementId, PlannerNodeType.Document,
            request.TemplateVersionId, cancellationToken);
    }
}

public sealed class UploadPlannerDocumentCommandHandler : IRequestHandler<UploadPlannerDocumentCommand, Guid>
{
    private readonly PlannerResourceCommandContext _context;
    private readonly IObjectStorage _storage;
    private readonly IPlannerAssetScanner _scanner;
    public UploadPlannerDocumentCommandHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        IPlannerResourceRepository resources, IPlannerTemplateRepository templates,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, IObjectStorage storage,
        IPlannerAssetScanner scanner)
    { _context = new(projects, boards, resources, templates, currentUser, unitOfWork); _storage = storage; _scanner = scanner; }

    public async Task<Guid> Handle(UploadPlannerDocumentCommand request, CancellationToken cancellationToken)
    {
        var safeName = PlannerResourcePolicy.ValidateAndSanitize(request.FileName, request.ContentType, request.Length);
        await using var buffer = new MemoryStream((int)request.Length);
        await request.Content.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length != request.Length || buffer.Length > PlannerResourcePolicy.MaxFileSize)
            throw new BusinessException("PLANNER_FILE_SIZE_MISMATCH", "The uploaded file size was invalid.");
        var bytes = buffer.ToArray();
        PlannerResourcePolicy.EnsureContentMatchesType(request.ContentType, safeName, bytes);
        var board = await _context.GetBoardAsync(request.ProjectId, cancellationToken);
        var resource = PlannerResource.CreateDocument(board.Id, board.ProjectId, board.OwnerUserId, request.Title);
        var assetId = Guid.NewGuid();
        var storageKey = $"planner/{board.OwnerUserId}/{board.ProjectId}/{resource.Id}/{assetId}";
        var asset = new PlannerAsset(resource.Id, board.Id, board.ProjectId, storageKey, safeName,
            request.ContentType, bytes.LongLength, PlannerResourcePolicy.Sha256(bytes), board.OwnerUserId);
        resource.AttachAsset(asset);

        await _storage.UploadAsync(storageKey, new MemoryStream(bytes, writable: false), request.ContentType, cancellationToken);
        try
        {
            var status = await _scanner.ScanAsync(storageKey, cancellationToken);
            asset.SetScanStatus(status);
            if (status != PlannerAssetScanStatus.Clean)
                throw new BusinessException("PLANNER_FILE_SCAN_FAILED", "The file did not pass the security scan.");
            return await _context.AddAsync(board, resource, request.ElementId, PlannerNodeType.Document,
                request.TemplateVersionId, cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(storageKey, CancellationToken.None);
            throw;
        }
    }
}

public sealed class LinkPlannerResourceCommandHandler : IRequestHandler<LinkPlannerResourceCommand, Guid>
{
    private readonly PlannerResourceCommandContext _context;
    public LinkPlannerResourceCommandHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        IPlannerResourceRepository resources, IPlannerTemplateRepository templates,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork) =>
        _context = new(projects, boards, resources, templates, currentUser, unitOfWork);

    public async Task<Guid> Handle(LinkPlannerResourceCommand request, CancellationToken cancellationToken)
    {
        var board = await _context.GetBoardAsync(request.ProjectId, cancellationToken);
        var resource = await _context.GetResourceAsync(request.ProjectId, request.ResourceId, cancellationToken);
        if (board.Nodes.Any(x => x.ResourceId == resource.Id))
            throw new ConflictException("PLANNER_RESOURCE_ALREADY_LINKED", "This resource already has a canvas card.");
        var type = resource.Kind == PlannerResourceKind.Note ? PlannerNodeType.Note : PlannerNodeType.Document;
        var node = board.LinkResource(request.ElementId, resource, type);
        var version = await PlannerTemplateAccess.ResolveAsync(request.TemplateVersionId, type,
            _context.Templates, cancellationToken);
        if (version is not null) node.ApplyTemplate(version);
        _context.Boards.Update(board);
        await _context.UnitOfWork.SaveChangesAsync(cancellationToken);
        return node.Id;
    }
}

public sealed class UpdatePlannerResourceCommandHandler : IRequestHandler<UpdatePlannerResourceCommand>
{
    private readonly PlannerResourceCommandContext _context;
    public UpdatePlannerResourceCommandHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        IPlannerResourceRepository resources, IPlannerTemplateRepository templates,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork) =>
        _context = new(projects, boards, resources, templates, currentUser, unitOfWork);
    public async Task Handle(UpdatePlannerResourceCommand request, CancellationToken cancellationToken)
    {
        var resource = await _context.GetResourceAsync(request.ProjectId, request.ResourceId, cancellationToken);
        try { resource.Update(request.Title, request.Content, request.Url); }
        catch (ArgumentException exception) { throw new BusinessException("PLANNER_RESOURCE_INVALID", exception.Message); }
        if (resource.Asset is not null && request.FileName is not null)
            resource.Asset.Rename(PlannerResourcePolicy.ValidateAndSanitize(request.FileName,
                resource.Asset.ContentType, resource.Asset.Size));
        await _context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeletePlannerResourceCommandHandler : IRequestHandler<DeletePlannerResourceCommand>
{
    private readonly PlannerResourceCommandContext _context;
    private readonly IObjectStorage _storage;
    public DeletePlannerResourceCommandHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        IPlannerResourceRepository resources, IPlannerTemplateRepository templates,
        ICurrentUserService currentUser, IUnitOfWork unitOfWork, IObjectStorage storage)
    { _context = new(projects, boards, resources, templates, currentUser, unitOfWork); _storage = storage; }
    public async Task Handle(DeletePlannerResourceCommand request, CancellationToken cancellationToken)
    {
        var board = await _context.GetBoardAsync(request.ProjectId, cancellationToken);
        var resource = await _context.GetResourceAsync(request.ProjectId, request.ResourceId, cancellationToken);
        var node = board.Nodes.FirstOrDefault(x => x.ResourceId == resource.Id);
        if (node is not null) board.UnlinkNode(node.Id);
        _context.Resources.Remove(resource);
        _context.Boards.Update(board);
        await _context.UnitOfWork.SaveChangesAsync(cancellationToken);
        if (resource.Asset is not null) await _storage.DeleteAsync(resource.Asset.StorageKey, cancellationToken);
    }
}
