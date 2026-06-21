using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Exceptions;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.AcceptInvitation
{
    public sealed class AcceptInvitationCommandHandler
        : IRequestHandler<AcceptInvitationCommand>
    {
        private readonly IOrganizationInvitationRepository
            _organizationInvitationRepository;

        private readonly IOrganizationMemberRepository
            _organizationMemberRepository;

        private readonly IUserRepository
            _userRepository;

        private readonly IUnitOfWork
            _unitOfWork;

        public AcceptInvitationCommandHandler(
            IOrganizationInvitationRepository organizationInvitationRepository,
            IOrganizationMemberRepository organizationMemberRepository,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationInvitationRepository =
                organizationInvitationRepository;

            _organizationMemberRepository =
                organizationMemberRepository;

            _userRepository =
                userRepository;

            _unitOfWork =
                unitOfWork;
        }

        public async Task Handle(
            AcceptInvitationCommand request,
            CancellationToken cancellationToken)
        {
            var invitation =
                await _organizationInvitationRepository
                    .GetByIdAsync(
                        request.InvitationId,
                        cancellationToken);

            if (invitation is null)
            {
                throw new NotFoundException(
                    "INVITATION_NOT_FOUND",
                    "Invitation not found.");
            }

            if (invitation.ExpiryDate < DateTime.UtcNow)
            {
                throw new BadRequestException(
                    "INVITATION_EXPIRED",
                    "Invitation has expired.");
            }

            var user =
                await _userRepository.GetByEmailAsync(
                    new Domain.ValueObjects.Email(
                        invitation.Email),
                    cancellationToken);

            if (user is null)
            {
                throw new NotFoundException(
                    "USER_NOT_FOUND",
                    "User account not found.");
            }

            var alreadyMember =
                await _organizationMemberRepository
                    .ExistsAsync(
                        invitation.OrganizationId,
                        user.Id,
                        cancellationToken);

            if (alreadyMember)
            {
                throw new ConflictException(
                    "USER_ALREADY_MEMBER",
                    "User is already a member.");
            }

            invitation.Accept();

            var member =
                new Domain.Entities.Organization.OrganizationMember(
                    invitation.OrganizationId,
                    user.Id,
                    invitation.OrganizationRoleId);

            await _organizationMemberRepository
                .AddAsync(
                    member,
                    cancellationToken);

            _organizationInvitationRepository
                .Update(invitation);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}