using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.ActivateMember
{
    public sealed class ActivateMemberCommandHandler
        : IRequestHandler<ActivateMemberCommand>
    {
        private readonly IOrganizationMemberRepository
            _organizationMemberRepository;

        private readonly IOrganizationPermissionChecker
            _permissionChecker;

        private readonly ICurrentUserService
            _currentUserService;

        private readonly IUnitOfWork
            _unitOfWork;

        public ActivateMemberCommandHandler(
            IOrganizationMemberRepository organizationMemberRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _organizationMemberRepository =
                organizationMemberRepository;

            _permissionChecker =
                permissionChecker;

            _currentUserService =
                currentUserService;

            _unitOfWork =
                unitOfWork;
        }

        public async Task Handle(
            ActivateMemberCommand request,
            CancellationToken cancellationToken)
        {
            // Phase 10: this handler previously enforced nothing.
            await _permissionChecker.EnsurePermissionAsync(
                request.OrganizationId,
                _currentUserService.UserId,
                OrganizationPermissionNames.ManageMembers,
                cancellationToken);

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