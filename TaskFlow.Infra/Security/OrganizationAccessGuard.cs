using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Security
{
    public sealed class OrganizationAccessGuard
        : IOrganizationAccessGuard
    {
        private readonly TaskFlowDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public OrganizationAccessGuard(
            TaskFlowDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task EnsureOrganizationAsync(
            int organizationId,
            CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;

            if (!await HasOrganizationAccessAsync(
                    userId, organizationId, cancellationToken))
            {
                throw Denied();
            }
        }

        public async Task EnsureProjectAsync(
            int projectId,
            CancellationToken cancellationToken = default)
        {
            var organizationId =
                await _context.Projects
                    .AsNoTracking()
                    .Where(x => x.Id == projectId)
                    .Select(x => (int?)x.OrganizationId)
                    .FirstOrDefaultAsync(cancellationToken);

            if (organizationId is null)
            {
                throw new NotFoundException(
                    "PROJECT_NOT_FOUND",
                    "Project not found.");
            }

            await EnsureOrganizationAsync(
                organizationId.Value, cancellationToken);
        }

        public async Task EnsureTaskAsync(
            int taskId,
            CancellationToken cancellationToken = default)
        {
            var task =
                await _context.Tasks
                    .AsNoTracking()
                    .Where(x => x.Id == taskId)
                    .Select(x => new
                    {
                        x.OrganizationId,
                        x.CreatedByUserId
                    })
                    .FirstOrDefaultAsync(cancellationToken);

            if (task is null)
            {
                throw new NotFoundException(
                    "TASK_NOT_FOUND",
                    "Task not found.");
            }

            if (task.OrganizationId is int organizationId)
            {
                await EnsureOrganizationAsync(
                    organizationId, cancellationToken);

                return;
            }

            // Personal task (no organization): only its creator
            // may see it.
            if (task.CreatedByUserId != _currentUserService.UserId)
            {
                throw Denied();
            }
        }

        public async Task EnsureTeamAsync(
            int teamId,
            CancellationToken cancellationToken = default)
        {
            var organizationId =
                await _context.Teams
                    .AsNoTracking()
                    .Where(x => x.Id == teamId)
                    .Select(x => (int?)x.OrganizationId)
                    .FirstOrDefaultAsync(cancellationToken);

            if (organizationId is null)
            {
                throw new NotFoundException(
                    "TEAM_NOT_FOUND",
                    "Team not found.");
            }

            await EnsureOrganizationAsync(
                organizationId.Value, cancellationToken);
        }

        public async Task EnsureRoleAsync(
            int organizationRoleId,
            CancellationToken cancellationToken = default)
        {
            var organizationId =
                await _context.OrganizationRoles
                    .AsNoTracking()
                    .Where(x => x.Id == organizationRoleId)
                    .Select(x => (int?)x.OrganizationId)
                    .FirstOrDefaultAsync(cancellationToken);

            if (organizationId is null)
            {
                throw new NotFoundException(
                    "ROLE_NOT_FOUND",
                    "Organization role not found.");
            }

            await EnsureOrganizationAsync(
                organizationId.Value, cancellationToken);
        }

        public async Task EnsureUserAsync(
            int targetUserId,
            CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;

            if (targetUserId == userId)
                return;

            // Visible if the two users share an organization
            // (either as owner or active member).
            var sharesOrganization =
                await _context.Organizations
                    .AsNoTracking()
                    .AnyAsync(o =>
                        (o.OwnerUserId == userId
                            || _context.OrganizationMembers.Any(m =>
                                m.OrganizationId == o.Id
                                && m.UserId == userId
                                && m.IsActive))
                        && (o.OwnerUserId == targetUserId
                            || _context.OrganizationMembers.Any(m =>
                                m.OrganizationId == o.Id
                                && m.UserId == targetUserId
                                && m.IsActive)),
                        cancellationToken);

            if (!sharesOrganization)
            {
                throw Denied();
            }
        }

        public async Task EnsureMemberReportAsync(
            int targetUserId,
            CancellationToken cancellationToken = default)
        {
            var userId = _currentUserService.UserId;

            if (targetUserId == userId)
                return;

            // The owner of an organization the target belongs to
            // may view that member's report.
            var isOwnerOfSharedOrganization =
                await _context.Organizations
                    .AsNoTracking()
                    .AnyAsync(o =>
                        o.OwnerUserId == userId
                        && _context.OrganizationMembers.Any(m =>
                            m.OrganizationId == o.Id
                            && m.UserId == targetUserId
                            && m.IsActive),
                        cancellationToken);

            if (!isOwnerOfSharedOrganization)
            {
                throw Denied();
            }
        }

        private async Task<bool> HasOrganizationAccessAsync(
            int userId,
            int organizationId,
            CancellationToken cancellationToken)
        {
            var isOwner =
                await _context.Organizations
                    .AsNoTracking()
                    .AnyAsync(o =>
                        o.Id == organizationId
                        && o.OwnerUserId == userId,
                        cancellationToken);

            if (isOwner)
                return true;

            return await _context.OrganizationMembers
                .AsNoTracking()
                .AnyAsync(m =>
                    m.OrganizationId == organizationId
                    && m.UserId == userId
                    && m.IsActive,
                    cancellationToken);
        }

        private static ForbiddenException Denied()
        {
            return new ForbiddenException(
                "ACCESS_DENIED",
                "You do not have access to this resource.");
        }
    }
}
