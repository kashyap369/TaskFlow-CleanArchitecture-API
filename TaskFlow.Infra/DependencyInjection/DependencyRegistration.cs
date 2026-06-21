using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.DomainEvents;
using TaskFlow.Application.DomainEvents.Identity.User;
using TaskFlow.Domain.DomainEvents.Identity.User;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Infra.DomainEvents.Dispatchers;
using TaskFlow.Infra.Email;
using TaskFlow.Infra.Email.Smtp;
using TaskFlow.Infra.Persistence;
using TaskFlow.Infra.Persistence.Context;
using TaskFlow.Infra.Persistence.Repositories.Identity.Users;
using TaskFlow.Infra.Persistence.Repositories.Organizations;
using TaskFlow.Infra.Persistence.Repositories.WorkManagement;
using TaskFlow.Infra.Security;

namespace TaskFlow.Infra.DependencyInjection
{
    public static class DependencyRegistration
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<TaskFlowDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"));
            });

            services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IDomainEventHandler<UserRegisteredEvent>, UserRegisteredEventHandler>();
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            // Register the organization repositories
            services.AddScoped<
    IOrganizationRepository,
    OrganizationRepository>();

            services.AddScoped<
                IOrganizationRoleRepository,
                OrganizationRoleRepository>();

            services.AddScoped<
                IOrganizationMemberRepository,
                OrganizationMemberRepository>();

            services.AddScoped<
                IOrganizationInvitationRepository,
                OrganizationInvitationRepository>();

            // Register the Wrok management  repositories
            services.AddScoped<
    IProjectRepository,
    ProjectRepository>();

            services.AddScoped<
                ITaskRepository,
                TaskRepository>();

            services.AddScoped<
                ISubTaskRepository,
                SubTaskRepository>();

            services.AddScoped<SmtpEmailSender>();

            services.AddScoped<IEmailService, EmailService>();
            var jwtSettings = configuration
                .GetSection("JwtSettings")
                .Get<JwtSettings>()
                ?? throw new InvalidOperationException("JwtSettings configuration is missing.");

            services.AddAuthentication(
                    JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters =
                        new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,

                            ValidIssuer = jwtSettings.Issuer,

                            ValidAudience = jwtSettings.Audience,

                            IssuerSigningKey =
                                new SymmetricSecurityKey(
                                    Encoding.UTF8.GetBytes(
                                        jwtSettings.SecretKey)),

                            ClockSkew = TimeSpan.Zero
                        };
                });

            services.AddScoped<IJwtProvider, JwtProvider>();

            return services;
        }
    }
}