using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Commands.CancelInvitation
{
    public sealed class CancelInvitationCommandHandler
        : IRequestHandler<CancelInvitationCommand>
    {
        private readonly IOrganizationInvitationRepository
            _organizationInvitationRepository;

        private readonly IUnitOfWork
            _unitOfWork;

        public CancelInvitationCommandHandler(
            IOrganizationInvitationRepository organizationInvitationRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationInvitationRepository =
                organizationInvitationRepository;

            _unitOfWork =
                unitOfWork;
        }

        public async Task Handle(
            CancelInvitationCommand request,
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

            invitation.Cancel();

            _organizationInvitationRepository
                .Update(invitation);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}