using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Amazon.Runtime;
using Amazon.S3;
using System.Text;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Application.Contracts.Configuration;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.DomainEvents;
using TaskFlow.Application.DomainEvents.Identity.User;
using TaskFlow.Application.DomainEvents.Organizations;
using TaskFlow.Domain.DomainEvents.Identity.User;
using TaskFlow.Domain.DomainEvents.Organizations;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Platform;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Infra.DomainEvents.Dispatchers;
using TaskFlow.Infra.Configuration;
using TaskFlow.Infra.Email;
using TaskFlow.Infra.Email.Smtp;
using TaskFlow.Infra.Persistence;
using TaskFlow.Infra.Persistence.Context;
using TaskFlow.Infra.Persistence.Repositories.Identity.Users;
using TaskFlow.Infra.Persistence.Repositories.Organizations;
using TaskFlow.Infra.Persistence.Repositories.Platform;
using TaskFlow.Infra.Persistence.Repositories.WorkManagement;
using TaskFlow.Infra.Security;
using TaskFlow.Infra.Storage;

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
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<ISystemRoleRepository, SystemRoleRepository>();
            services.AddScoped<IUserRoleRepository, UserRoleRepository>();
            services.AddScoped<IDomainEventHandler<UserRegisteredEvent>, UserRegisteredEventHandler>();
            services.AddScoped<IDomainEventHandler<OrganizationMemberInvitedEvent>, OrganizationMemberInvitedEventHandler>();
            services.AddScoped<IOrganizationPermissionChecker, OrganizationPermissionChecker>();
            services.AddScoped<IOrganizationAccessGuard, OrganizationAccessGuard>();
            // Stateless (HMAC) — no per-request state, so a singleton is fine.
            services.AddSingleton<
                IEmailVerificationTokenService,
                EmailVerificationTokenService>();

            // Read side (Dapper): a connection factory the query
            // handlers use to run raw SQL straight into DTOs.
            services.AddSingleton<ISqlConnectionFactory, TaskFlow.Infra.Dapper.SqlConnectionFactory>();

            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services
                .AddOptions<ClientSettings>()
                .Bind(configuration.GetSection("ClientSettings"))
                .Validate(
                    settings => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
                    "ClientSettings:BaseUrl must be an absolute URL.")
                .ValidateOnStart();
            services.AddSingleton<IClientUrlProvider, ClientUrlProvider>();
            services
                .AddOptions<ObjectStorageSettings>()
                .Bind(configuration.GetSection("ObjectStorage"))
                .Validate(
                    settings => Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _),
                    "ObjectStorage:Endpoint must be an absolute URL.")
                .Validate(
                    settings =>
                        !string.IsNullOrWhiteSpace(settings.AccessKey) &&
                        !string.IsNullOrWhiteSpace(settings.SecretKey) &&
                        !string.IsNullOrWhiteSpace(settings.Bucket),
                    "Object storage credentials and bucket are required.")
                .ValidateOnStart();
            services.AddSingleton<IAmazonS3>(serviceProvider =>
            {
                var settings = serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<ObjectStorageSettings>>()
                    .Value;

                var credentials = new BasicAWSCredentials(
                    settings.AccessKey,
                    settings.SecretKey);

                return new AmazonS3Client(
                    credentials,
                    new AmazonS3Config
                    {
                        ServiceURL = settings.Endpoint,
                        ForcePathStyle = settings.ForcePathStyle,
                        UseHttp = !settings.UseSsl,
                        AuthenticationRegion = "us-east-1"
                    });
            });
            services.AddSingleton<IObjectStorage, S3ObjectStorage>();
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

            services.AddScoped<
                ITeamRepository,
                TeamRepository>();

            services.AddScoped<
                IOrganizationPermissionRepository,
                OrganizationPermissionRepository>();

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

            services.AddScoped<
                ITaskWorkLogRepository,
                TaskWorkLogRepository>();

            services.AddScoped<
                IPlatformSettingRepository,
                PlatformSettingRepository>();

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
