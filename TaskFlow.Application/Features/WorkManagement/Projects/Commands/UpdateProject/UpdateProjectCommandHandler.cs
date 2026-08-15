using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.UpdateProject
{
    public sealed class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand>
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProjectCommandHandler(
            IProjectRepository projectRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _projectRepository = projectRepository;
            _permissionChecker = permissionChecker;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UpdateProjectCommand request,
            CancellationToken cancellationToken)
        {
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

            await _permissionChecker.EnsurePermissionAsync(
                project.OrganizationId,
                _currentUserService.UserId,
                OrganizationPermissionNames.ManageProjects,
                cancellationToken);

            var existingProject =
                await _projectRepository.GetByNameAsync(
                    project.OrganizationId,
                    request.Title,
                    cancellationToken);

            if (existingProject is not null &&
                existingProject.Id != project.Id)
            {
                throw new ConflictException(
                    "PROJECT_ALREADY_EXISTS",
                    "Project with same name already exists.");
            }

            project.UpdateDetails(
                request.Title,
                request.Description,
                request.ExpectedCompletionDate);

            _projectRepository.Update(project);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
