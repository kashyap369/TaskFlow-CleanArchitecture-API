using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Security;

namespace TaskFlow.Application.Behaviors
{
    /// <summary>
    /// Read-side authorization gate. Runs after validation and
    /// before the handler: if the request is marked with one of
    /// the access-scoped interfaces, the matching guard check runs
    /// first and throws when the current user may not see the
    /// resource. Unmarked requests (all commands, and the
    /// current-user "my …" queries) pass straight through.
    /// </summary>
    public sealed class AccessGuardBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IOrganizationAccessGuard _accessGuard;

        public AccessGuardBehavior(
            IOrganizationAccessGuard accessGuard)
        {
            _accessGuard = accessGuard;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            switch (request)
            {
                case IOrganizationScopedRequest r:
                    await _accessGuard.EnsureOrganizationAsync(
                        r.OrganizationId, cancellationToken);
                    break;

                case IProjectScopedRequest r:
                    await _accessGuard.EnsureProjectAsync(
                        r.ProjectId, cancellationToken);
                    break;

                case ITaskScopedRequest r:
                    await _accessGuard.EnsureTaskAsync(
                        r.TaskId, cancellationToken);
                    break;

                case ITeamScopedRequest r:
                    await _accessGuard.EnsureTeamAsync(
                        r.TeamId, cancellationToken);
                    break;

                case IRoleScopedRequest r:
                    await _accessGuard.EnsureRoleAsync(
                        r.OrganizationRoleId, cancellationToken);
                    break;

                case IMemberReportScopedRequest r:
                    await _accessGuard.EnsureMemberReportAsync(
                        r.UserId, cancellationToken);
                    break;

                case IUserScopedRequest r:
                    await _accessGuard.EnsureUserAsync(
                        r.UserId, cancellationToken);
                    break;
            }

            return await next();
        }
    }
}
