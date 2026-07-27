using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.RemoveMember
{
    public sealed class RemoveMemberCommandHandler
        : IRequestHandler<RemoveMemberCommand>
    {
        private readonly IOrganizationMemberRepository
            _organizationMemberRepository;

        private readonly IOrganizationPermissionChecker
            _permissionChecker;

        private readonly ICurrentUserService
            _currentUserService;

        private readonly IUnitOfWork
            _unitOfWork;

        public RemoveMemberCommandHandler(
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
            RemoveMemberCommand request,
            CancellationToken cancellationToken)
        {
            // Before Phase 10 this handler enforced nothing: any
            // authenticated user could remove any member of any
            // organization by guessing ids.
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

            // Remove really removes (soft delete — the global query
            // filter hides the row). This used to call Deactivate(),
            // which made it byte-identical to DeactivateMemberCommand:
            // a "removed" member stayed in the list as Inactive.
            // The two commands are now genuinely different — Deactivate
            // is a reversible toggle, Remove is not.
            _organizationMemberRepository.Remove(
                member);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
