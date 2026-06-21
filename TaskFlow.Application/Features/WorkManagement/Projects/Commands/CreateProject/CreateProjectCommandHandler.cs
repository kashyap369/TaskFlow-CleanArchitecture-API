using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreateProject
{
    public sealed class CreateProjectCommandHandler
        : IRequestHandler<CreateProjectCommand, int>
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateProjectCommandHandler(
            IProjectRepository projectRepository,
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            CreateProjectCommand request,
            CancellationToken cancellationToken)
        {
            var organization =
                await _organizationRepository.GetByIdAsync(
                    request.OrganizationId,
                    cancellationToken);

            if (organization is null)
            {
                throw new NotFoundException(
                    "ORGANIZATION_NOT_FOUND",
                    "Organization not found.");
            }

            var user =
                await _userRepository.GetByIdAsync(
                    request.CreatedByUserId,
                    cancellationToken);

            if (user is null)
            {
                throw new NotFoundException(
                    "USER_NOT_FOUND",
                    "User not found.");
            }

            var projectExists =
                await _projectRepository.ExistsByNameAsync(
                    request.OrganizationId,
                    request.Title,
                    cancellationToken);

            if (projectExists)
            {
                throw new ConflictException(
                    "PROJECT_ALREADY_EXISTS",
                    "Project already exists.");
            }

            var project = new Project(
                request.Title,
                request.Description,
                request.StartDate,
                request.OrganizationId,
                request.CreatedByUserId,
                request.ExpectedCompletionDate);

            await _projectRepository.AddAsync(
                project,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return project.Id;
        }
    }
}