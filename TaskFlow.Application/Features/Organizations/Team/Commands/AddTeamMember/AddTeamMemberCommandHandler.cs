using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.Team.Commands.AddTeamMember
{
    public sealed class AddTeamMemberCommandHandler
        : IRequestHandler<AddTeamMemberCommand>
    {
        private readonly ITeamRepository _teamRepository;
        private readonly IOrganizationMemberRepository _organizationMemberRepository;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public AddTeamMemberCommandHandler(
            ITeamRepository teamRepository,
            IOrganizationMemberRepository organizationMemberRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _teamRepository = teamRepository;
            _organizationMemberRepository = organizationMemberRepository;
            _permissionChecker = permissionChecker;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            AddTeamMemberCommand request,
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

            // Only people who belong to the organization can be
            // placed on one of its teams.
            var isActiveMember =
                await _organizationMemberRepository
                    .IsActiveMemberAsync(
                        team.OrganizationId,
                        request.UserId,
                        cancellationToken);

            if (!isActiveMember)
            {
                throw new ConflictException(
                    "NOT_AN_ORGANIZATION_MEMBER",
                    "User is not an active member of the organization.");
            }

            team.AddMember(request.UserId);

            _teamRepository.Update(team);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
