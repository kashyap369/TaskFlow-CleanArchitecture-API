using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

using TeamEntity = TaskFlow.Domain.Entities.Organization.Team;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.CreateTeam
{
    public sealed class CreateTeamCommandHandler
        : IRequestHandler<CreateTeamCommand, int>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public CreateTeamCommandHandler(
            IOrganizationRepository organizationRepository,
            ITeamRepository teamRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _teamRepository = teamRepository;
            _permissionChecker = permissionChecker;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            CreateTeamCommand request,
            CancellationToken cancellationToken)
        {
            var organizationExists =
                await _organizationRepository.ExistsAsync(
                    request.OrganizationId,
                    cancellationToken);

            if (!organizationExists)
            {
                throw new NotFoundException(
                    "ORGANIZATION_NOT_FOUND",
                    "Organization not found.");
            }

            await _permissionChecker.EnsurePermissionAsync(
                request.OrganizationId,
                _currentUserService.UserId,
                OrganizationPermissionNames.ManageTeams,
                cancellationToken);

            var nameExists =
                await _teamRepository.ExistsByNameAsync(
                    request.OrganizationId,
                    request.Name,
                    cancellationToken);

            if (nameExists)
            {
                throw new ConflictException(
                    "TEAM_ALREADY_EXISTS",
                    "A team with the same name already exists.");
            }

            var team = new TeamEntity(
                request.OrganizationId,
                request.Name,
                request.Description,
                _currentUserService.UserId);

            await _teamRepository.AddAsync(
                team,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return team.Id;
        }
    }
}
