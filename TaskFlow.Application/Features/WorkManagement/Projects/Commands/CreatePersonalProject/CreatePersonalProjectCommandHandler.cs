using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreatePersonalProject;

public sealed class CreatePersonalProjectCommandHandler
    : IRequestHandler<CreatePersonalProjectCommand, int>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePersonalProjectCommandHandler(
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(
        CreatePersonalProjectCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(
                "USER_NOT_FOUND",
                "User not found.");
        }

        if (await _projectRepository.ExistsPersonalByNameAsync(
                userId,
                request.Title,
                cancellationToken))
        {
            throw new ConflictException(
                "PROJECT_ALREADY_EXISTS",
                "A personal project with the same title already exists.");
        }

        var project = new Project(
            request.Title,
            request.Description,
            request.StartDate,
            organizationId: null,
            createdByUserId: userId,
            request.ExpectedCompletionDate);

        await _projectRepository.AddAsync(project, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return project.Id;
    }
}
