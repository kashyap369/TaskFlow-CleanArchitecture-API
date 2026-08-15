using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.DeleteProject
{
    public sealed class DeleteProjectCommandHandler
        : IRequestHandler<DeleteProjectCommand>
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteProjectCommandHandler(
            IProjectRepository projectRepository,
            IOrganizationPermissionChecker permissionChecker,
            IOrganizationAccessGuard accessGuard,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _projectRepository = projectRepository;
            _permissionChecker = permissionChecker;
            _accessGuard = accessGuard;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteProjectCommand request,
            CancellationToken cancellationToken)
        {
            await _accessGuard.EnsureProjectAsync(
                request.ProjectId,
                cancellationToken);

            var project =
                await _projectRepository.GetByIdAsync(
                    request.ProjectId,
                    cancellationToken);

            if (project is null)
            {
                throw new NotFoundException(
                    "PROJECT_NOT_FOUND",
                    "Project not found.");
            }

            if (project.OrganizationId is int organizationId)
            {
                await _permissionChecker.EnsurePermissionAsync(
                    organizationId,
                    _currentUserService.UserId,
                    OrganizationPermissionNames.ManageProjects,
                    cancellationToken);
            }

            _projectRepository.Remove(project);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
