using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.DeleteProject
{
    public sealed class DeleteProjectCommandHandler
        : IRequestHandler<DeleteProjectCommand>
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteProjectCommandHandler(
            IProjectRepository projectRepository,
            IUnitOfWork unitOfWork)
        {
            _projectRepository = projectRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteProjectCommand request,
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

            _projectRepository.Remove(project);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}