using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.DeleteTeam
{
    public sealed class DeleteTeamCommandHandler
        : IRequestHandler<DeleteTeamCommand>
    {
        private readonly ITeamRepository _teamRepository;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteTeamCommandHandler(
            ITeamRepository teamRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _teamRepository = teamRepository;
            _permissionChecker = permissionChecker;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteTeamCommand request,
            CancellationToken cancellationToken)
        {
            var team =
                await _teamRepository.GetByIdAsync(
                    request.TeamId,
                    cancellationToken);

            if (team is null)
            {
                throw new NotFoundException(
                    "TEAM_NOT_FOUND",
                    "Team not found.");
            }

            await _permissionChecker.EnsurePermissionAsync(
                team.OrganizationId,
                _currentUserService.UserId,
                OrganizationPermissionNames.ManageTeams,
                cancellationToken);

            _teamRepository.Remove(team);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
