using TaskFlow.Api.Constants;
using TaskFlow.Domain.Constants;

namespace TaskFlow.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// The single place that decides which roles can use
        /// which policy. To change access rules, edit the
        /// RequireRole(...) lists below — nothing else.
        ///
        /// Usage on a controller or action:
        ///   [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        /// </summary>
        public static IServiceCollection AddAuthorizationPolicies(
            this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(
                    AuthorizationPolicies.AdminOnly,
                    policy => policy.RequireRole(
                        SystemRoleNames.Admin));

                options.AddPolicy(
                    AuthorizationPolicies.ManagerAndAbove,
                    policy => policy.RequireRole(
                        SystemRoleNames.Admin,
                        SystemRoleNames.Manager));

                options.AddPolicy(
                    AuthorizationPolicies.AllRoles,
                    policy => policy.RequireRole(
                        SystemRoleNames.Admin,
                        SystemRoleNames.Manager,
                        SystemRoleNames.User));
            });

            return services;
        }
    }
}
