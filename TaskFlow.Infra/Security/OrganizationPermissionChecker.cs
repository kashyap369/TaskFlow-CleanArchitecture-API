using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Security
{
    public sealed class OrganizationPermissionChecker
        : IOrganizationPermissionChecker
    {
        private readonly TaskFlowDbContext _context;

        public OrganizationPermissionChecker(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasPermissionAsync(
            int organizationId,
            int userId,
            string permissionName,
            CancellationToken cancellationToken = default)
        {
            // The owner implicitly has every permission.
            var isOwner =
                await _context.Organizations
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.Id == organizationId
                          && x.OwnerUserId == userId,
                        cancellationToken);

            if (isOwner)
                return true;

            // Otherwise: the user must be an active member whose
            // role has been granted the named permission.
            var query =
                from member in _context.OrganizationMembers.AsNoTracking()
                join rolePermission in _context.OrganizationRolePermissions.AsNoTracking()
                    on member.OrganizationRoleId equals rolePermission.OrganizationRoleId
                join permission in _context.OrganizationPermissions.AsNoTracking()
                    on rolePermission.OrganizationPermissionId equals permission.Id
                where member.OrganizationId == organizationId
                   && member.UserId == userId
                   && member.IsActive
                   && permission.Name == permissionName
                select member.Id;

            return await query.AnyAsync(cancellationToken);
        }

        public async Task EnsurePermissionAsync(
            int organizationId,
            int userId,
            string permissionName,
            CancellationToken cancellationToken = default)
        {
            var allowed =
                await HasPermissionAsync(
                    organizationId,
                    userId,
                    permissionName,
                    cancellationToken);

            if (!allowed)
            {
                throw new ForbiddenException(
                    "PERMISSION_DENIED",
                    $"You do not have the '{permissionName}' permission " +
                    "in this organization.");
            }
        }
    }
}
