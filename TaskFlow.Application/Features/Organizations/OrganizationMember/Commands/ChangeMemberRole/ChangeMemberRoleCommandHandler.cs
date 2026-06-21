using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ChangeMemberRole
{
    public sealed class ChangeMemberRoleCommandHandler
        : IRequestHandler<ChangeMemberRoleCommand>
    {
        private readonly IOrganizationMemberRepository
            _organizationMemberRepository;

        private readonly IOrganizationRoleRepository
            _organizationRoleRepository;

        private readonly IUnitOfWork
            _unitOfWork;

        public ChangeMemberRoleCommandHandler(
            IOrganizationMemberRepository organizationMemberRepository,
            IOrganizationRoleRepository organizationRoleRepository,
            IUnitOfWork unitOfWork)
        {
            _organizationMemberRepository =
                organizationMemberRepository;

            _organizationRoleRepository =
                organizationRoleRepository;

            _unitOfWork =
                unitOfWork;
        }

        public async Task Handle(
            ChangeMemberRoleCommand request,
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

            var role =
                await _organizationRoleRepository
                    .GetByIdAsync(
                        request.OrganizationRoleId,
                        cancellationToken);

            if (role is null)
            {
                throw new NotFoundException(
                    "ROLE_NOT_FOUND",
                    "Organization role not found.");
            }

            member.ChangeRole(
                request.OrganizationRoleId);

            _organizationMemberRepository.Update(
                member);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}