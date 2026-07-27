using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
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

        private readonly IOrganizationPermissionChecker
            _permissionChecker;

        private readonly ICurrentUserService
            _currentUserService;

        private readonly IUnitOfWork
            _unitOfWork;

        public ChangeMemberRoleCommandHandler(
            IOrganizationMemberRepository organizationMemberRepository,
            IOrganizationRoleRepository organizationRoleRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _organizationMemberRepository =
                organizationMemberRepository;

            _organizationRoleRepository =
                organizationRoleRepository;

            _permissionChecker =
                permissionChecker;

            _currentUserService =
                currentUserService;

            _unitOfWork =
                unitOfWork;
        }

        public async Task Handle(
            ChangeMemberRoleCommand request,
            CancellationToken cancellationToken)
        {
            // Phase 10: this handler previously enforced nothing —
            // any authenticated user could re-role any member.
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

            // The role must belong to the same organization as the
            // member. Without this, a role id from *another*
            // organization was accepted, silently granting that
            // org's permission set inside this one.
            if (role.OrganizationId != request.OrganizationId)
            {
                throw new ConflictException(
                    "ROLE_ORGANIZATION_MISMATCH",
                    "The role does not belong to this organization.");
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