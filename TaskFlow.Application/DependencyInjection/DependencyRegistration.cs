using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Behaviors;

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

            return services;
        }
    }
}