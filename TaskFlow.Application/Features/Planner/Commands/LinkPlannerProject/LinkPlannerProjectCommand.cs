using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Commands.LinkPlannerProject;

public sealed record LinkPlannerProjectCommand(int ProjectId, string ElementId, Guid? TemplateVersionId = null) : IRequest<Guid>;

public sealed class LinkPlannerProjectCommandHandler : IRequestHandler<LinkPlannerProjectCommand, Guid>
{
    private readonly IProjectRepository _projects;
    private readonly IPlannerBoardRepository _boards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPlannerTemplateRepository _templates;
    private readonly TaskFlow.Application.Contracts.Security.ICurrentUserService _currentUser;

    public LinkPlannerProjectCommandHandler(IProjectRepository projects, IPlannerBoardRepository boards,
        IUnitOfWork unitOfWork, TaskFlow.Application.Contracts.Security.ICurrentUserService currentUser,
        IPlannerTemplateRepository templates)
    { _projects = projects; _boards = boards; _unitOfWork = unitOfWork; _currentUser = currentUser; _templates = templates; }

    public async Task<Guid> Handle(LinkPlannerProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        var board = await _boards.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");
        if (board.Nodes.Any(x => x.ElementId == request.ElementId || x.ProjectId == request.ProjectId))
            throw new ConflictException("PLANNER_NODE_ALREADY_LINKED", "This project or canvas element is already linked.");
        var node = board.LinkProject(request.ElementId, project);
        var version = await PlannerTemplateAccess.ResolveAsync(request.TemplateVersionId, Domain.Enums.Planner.PlannerNodeType.Project, _templates, cancellationToken);
        if (version is not null) node.ApplyTemplate(version);
        _boards.Update(board);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return node.Id;
    }
}
