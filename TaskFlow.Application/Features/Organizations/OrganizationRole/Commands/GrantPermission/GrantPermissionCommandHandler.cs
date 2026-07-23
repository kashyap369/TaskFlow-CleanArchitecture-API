using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Commands.GrantPermission
{
    public sealed class GrantPermissionCommandHandler
        : IRequestHandler<GrantPermissionCommand>
    {
        private readonly IOrganizationRoleRepository _organizationRoleRepository;
        private readonly IOrganizationPermissionRepository _organizationPermissionRepository;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public GrantPermissionCommandHandler(
            IOrganizationRoleRepository organizationRoleRepository,
            IOrganizationPermissionRepository organizationPermissionRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _organizationRoleRepository = organizationRoleRepository;
            _organizationPermissionRepository = organizationPermissionRepository;
            _permissionChecker = permissionChecker;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            GrantPermissionCommand request,
            CancellationToken cancellationToken)
        {
            var role =
                await _organizationRoleRepository
                    .GetByIdWithPermissionsAsync(
                        request.OrganizationRoleId,
                        cancellationToken);

            if (role is null)
            {
                throw new NotFoundException(
                    "ROLE_NOT_FOUND",
                    "Organization role not found.");
            }

            await _permissionChecker.EnsurePermissionAsync(
                role.OrganizationId,
                _currentUserService.UserId,
                OrganizationPermissionNames.ManageRoles,
                cancellationToken);

            var permission =
                await _organizationPermissionRepository
                    .GetByNameAsync(
                        request.PermissionName,
                        cancellationToken);

            if (permission is null)
            {
                throw new NotFoundException(
                    "PERMISSION_NOT_FOUND",
                    "Unknown permission.");
            }

            role.GrantPermission(permission.Id);

            _organizationRoleRepository.Update(role);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
