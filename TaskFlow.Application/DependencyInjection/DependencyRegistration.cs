using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Behaviors;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Planner;
using TaskFlow.Application.Features.Identity.User.Services;

namespace TaskFlow.Application.DependencyInjection
{
    public static class DependencyRegistration
    {
        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(
                    typeof(DependencyRegistration).Assembly);
            });

            services.AddValidatorsFromAssembly(
                typeof(DependencyRegistration).Assembly);

            services.AddTransient(
                typeof(IPipelineBehavior<,>),
                typeof(ValidationBehavior<,>));

            // Runs after validation; enforces read-side access
            // for requests marked with the access-scoped interfaces.
            services.AddTransient(
                typeof(IPipelineBehavior<,>),
                typeof(AccessGuardBehavior<,>));

            services.AddScoped<IAuthSessionIssuer, AuthSessionIssuer>();
            services.AddScoped<IRequirementChangeContext, RequirementChangeContext>();
            services.AddScoped<OneTimeCodeRequestService>();
            services.AddScoped<OneTimeCodeVerifier>();

            return services;
        }
    }
}
