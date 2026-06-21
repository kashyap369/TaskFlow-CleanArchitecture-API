using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.InviteUser
{
    public sealed class InviteUserCommandHandler
        : IRequestHandler<InviteUserCommand, int>
    {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationRoleRepository _organizationRoleRepository;
        private readonly IOrganizationInvitationRepository _organizationInvitationRepository;
        private readonly IUnitOfWork _unitOfWork;

        public InviteUserCommandHandler(
            IOrganizationRepository organizationRepository,
            IOrganizationRoleRepository organizationRoleRepository,
            IOrganizationInvitationRepository organizationInvitationRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationRepository = organizationRepository;
            _organizationRoleRepository = organizationRoleRepository;
            _organizationInvitationRepository = organizationInvitationRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            InviteUserCommand request,
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

            var role =
                await _organizationRoleRepository.GetByIdAsync(
                    request.OrganizationRoleId,
                    cancellationToken);

            if (role is null)
            {
                throw new NotFoundException(
                    "ROLE_NOT_FOUND",
                    "Organization role not found.");
            }

            var pendingInvitationExists =
                await _organizationInvitationRepository
                    .ExistsPendingInvitationAsync(
                        request.OrganizationId,
                        request.Email,
                        cancellationToken);

            if (pendingInvitationExists)
            {
                throw new ConflictException(
                    "INVITATION_ALREADY_EXISTS",
                    "A pending invitation already exists.");
            }

            var invitation =
                new Domain.Entities.Organization.OrganizationInvitation(
                    request.OrganizationId,
                    request.Email,
                    request.OrganizationRoleId,
                    request.InvitedByUserId,
                    DateTime.UtcNow.AddDays(7));

            await _organizationInvitationRepository
                .AddAsync(
                    invitation,
                    cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return invitation.Id;
        }
    }
}