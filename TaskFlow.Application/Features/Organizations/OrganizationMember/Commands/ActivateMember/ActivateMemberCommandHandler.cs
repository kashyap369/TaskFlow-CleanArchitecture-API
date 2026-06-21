using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ActivateMember
{
    public sealed class ActivateMemberCommandHandler
        : IRequestHandler<ActivateMemberCommand>
    {
        private readonly IOrganizationMemberRepository
            _organizationMemberRepository;

        private readonly IUnitOfWork
            _unitOfWork;

        public ActivateMemberCommandHandler(
            IOrganizationMemberRepository organizationMemberRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationMemberRepository =
                organizationMemberRepository;

            _unitOfWork =
                unitOfWork;
        }

        public async Task Handle(
            ActivateMemberCommand request,
            CancellationToken cancellationToken)
        {
            var member =
                await _organizationMemberRepository
                    .GetMemberAsync(
                        request.OrganizationId,
                        request.UserId,
                        cancellationToken);

            if (member is null)
            {
                throw new NotFoundException(
                    "MEMBER_NOT_FOUND",
                    "Organization member not found.");
            }

            member.Activate();

            _organizationMemberRepository.Update(
                member);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}