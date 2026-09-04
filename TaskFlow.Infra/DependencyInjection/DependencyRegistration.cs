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
using TaskFlow.Application.Contracts.Meetings;
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
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Platform;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Infra.DomainEvents.Dispatchers;
using TaskFlow.Infra.Configuration;
using TaskFlow.Infra.Email;
using TaskFlow.Infra.Email.Smtp;
using TaskFlow.Infra.Persistence;
using TaskFlow.Infra.Persistence.Context;
using TaskFlow.Infra.Persistence.Repositories.Identity.Users;
using TaskFlow.Infra.Persistence.Repositories.Meetings;
using TaskFlow.Infra.Persistence.Repositories.Organizations;
using TaskFlow.Infra.Persistence.Repositories.Platform;
using TaskFlow.Infra.Persistence.Repositories.Planner;
using TaskFlow.Infra.Persistence.Repositories.WorkManagement;
using TaskFlow.Infra.Security;
using TaskFlow.Infra.Storage;
using TaskFlow.Infra.Meetings;

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
            services.AddScoped<IOneTimeCodeRepository, OneTimeCodeRepository>();
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
            services
                .AddOptions<OneTimeCodeSettings>()
                .Bind(configuration.GetSection("OneTimeCodeSettings"))
                .PostConfigure(settings =>
                {
                    if (string.IsNullOrWhiteSpace(settings.SecretKey))
                    {
                        settings.SecretKey = configuration["JwtSettings:SecretKey"] ?? string.Empty;
                    }
                })
                .Validate(
                    settings => settings.SecretKey.Length >= 32,
                    "OneTimeCodeSettings:SecretKey must contain at least 32 characters.")
                .ValidateOnStart();
            services.AddSingleton<IOneTimeCodeProtector, OneTimeCodeProtector>();
            services.AddSingleton<IMeetingGuestCodeProtector, MeetingGuestCodeProtector>();

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
                    settings => settings.UsesLocalFileSystem ||
                        Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _),
                    "ObjectStorage:Endpoint must be an absolute URL.")
                .Validate(
                    settings => settings.UsesLocalFileSystem ||
                        !string.IsNullOrWhiteSpace(settings.AccessKey) &&
                        !string.IsNullOrWhiteSpace(settings.SecretKey) &&
                        !string.IsNullOrWhiteSpace(settings.Bucket),
                    "Object storage credentials and bucket are required.")
                .Validate(
                    settings => !settings.UsesLocalFileSystem ||
                        !string.IsNullOrWhiteSpace(settings.LocalPath),
                    "ObjectStorage:LocalPath is required for local storage.")
                .ValidateOnStart();

            var objectStorageSettings = configuration
                .GetSection("ObjectStorage")
                .Get<ObjectStorageSettings>() ?? new ObjectStorageSettings();

            if (objectStorageSettings.UsesLocalFileSystem)
            {
                services.AddSingleton<IObjectStorage, LocalFileObjectStorage>();
            }
            else
            {
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
            }
            services.AddSingleton<IPlannerAssetScanner, NoOpPlannerAssetScanner>();
            services
                .AddOptions<LiveKitSettings>()
                .Bind(configuration.GetSection(LiveKitSettings.SectionName))
                .Validate(
                    settings => !settings.Enabled ||
                        Uri.TryCreate(settings.Url, UriKind.Absolute, out var uri) &&
                        uri.Scheme is "ws" or "wss",
                    "LiveKit:Url must be an absolute ws:// or wss:// URL when LiveKit is enabled.")
                .Validate(
                    settings => !settings.Enabled ||
                        !string.IsNullOrWhiteSpace(settings.ApiKey) &&
                        settings.ApiSecret.Length >= 32,
                    "LiveKit API key and a secret of at least 32 characters are required when enabled.")
                .Validate(
                    settings => settings.WebhookToleranceSeconds is >= 30 and <= 900,
                    "LiveKit:WebhookToleranceSeconds must be between 30 and 900 seconds.")
                .ValidateOnStart();
            services.AddSingleton<IMeetingMediaProvider, LiveKitMeetingMediaProvider>();
            services.AddSingleton<IMeetingReadinessProbe, MeetingReadinessProbe>();
            services
                .AddOptions<MeetingSettings>()
                .Bind(configuration.GetSection(MeetingSettings.SectionName))
                .Validate(settings => settings.GuestSessionMinutes is >= 5 and <= 1440,
                    "Meetings:GuestSessionMinutes must be between 5 and 1440.")
                .Validate(settings => settings.DefaultRetentionDays is >= 1 and <= 3650,
                    "Meetings:DefaultRetentionDays must be between 1 and 3650.")
                .Validate(settings => settings.MaxFileBytes is >= 1_024 and <= 1_073_741_824,
                    "Meetings:MaxFileBytes must be between 1 KiB and 1 GiB.")
                .Validate(settings => !settings.GuestsEnabled || settings.Enabled,
                    "Meetings:GuestsEnabled requires Meetings:Enabled.")
                .Validate(settings => !settings.RecordingEnabled || settings.Enabled,
                    "Meetings:RecordingEnabled requires Meetings:Enabled.")
                .Validate(settings => settings.RecordingConsentTimeoutSeconds is >= 15 and <= 300,
                    "Meetings:RecordingConsentTimeoutSeconds must be between 15 and 300.")
                .ValidateOnStart();
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

            services.AddScoped<ICalendarEntryRepository, CalendarEntryRepository>();
            services.AddScoped<IMeetingRepository, MeetingRepository>();
            services.AddScoped<IMeetingGuestAccessRepository, MeetingGuestAccessRepository>();
            services.AddScoped<IMeetingCollaborationRepository, MeetingCollaborationRepository>();
            services.AddScoped<IMeetingRecordingRepository, MeetingRecordingRepository>();
            services.AddHostedService<MeetingRetentionCleanupService>();
            services.AddHostedService<MeetingRecordingRecoveryService>();

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

            services.AddScoped<IPlannerBoardRepository, PlannerBoardRepository>();
            services.AddScoped<IPlannerTemplateRepository, PlannerTemplateRepository>();
            services.AddScoped<IPlannerResourceRepository, PlannerResourceRepository>();
            services.AddScoped<IRequirementBaselineRepository, RequirementBaselineRepository>();

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
