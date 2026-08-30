using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;
using TaskFlow.Api.Extensions;
using TaskFlow.Api.Services;
using TaskFlow.Api.Filters;
using TaskFlow.Api.Options;
using TaskFlow.Api.Meetings;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.DependencyInjection;
using TaskFlow.Infra.DependencyInjection;
using TaskFlow.Infra.Persistence.Context;
using TaskFlow.Infra.Seeder.Identity.Role;
using TaskFlow.Infra.Seeder.Identity.User;
using TaskFlow.Infra.Seeder.Organization.Permission;
using TaskFlow.Infra.Seeder.Platform;

var builder = WebApplication.CreateBuilder(args);

// Console logging is portable across local Windows development, containers, and EF design-time tools.
// The Windows EventLog provider requires machine-level source permissions and prevented migrations.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Services

builder.Services.AddControllers();
builder.Services.Configure<PlannerOptions>(
    builder.Configuration.GetSection(PlannerOptions.SectionName));
builder.Services.AddScoped<PlannerFeatureFilter>();

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth-code", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("planner", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                          httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("planner-upload", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                          httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<MeetingWebhookReplayGuard>();

// Swagger with a Bearer token input, so protected
// endpoints can be tested from the Swagger UI.

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Paste the JWT from the login response."
        });

    options.AddSecurityRequirement(document =>
        new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference(
                    "Bearer",
                    document),
                new List<string>()
            }
        });
});

builder.Services.AddApplication();

builder.Services.AddInfrastructure(
    builder.Configuration);

// Current user (reads the user id and email from the JWT claims)

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Role based authorization policies (AdminOnly,
// ManagerAndAbove, ...) — see ServiceCollectionExtensions.

builder.Services.AddAuthorizationPolicies();
var allowedOrigins =
    builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
    ?? ["http://localhost:4200"];

// Keep the canonical production client reachable while older deployments
// are migrated away from the historical `tasflow` hostname typo.
allowedOrigins = allowedOrigins
    .Append("https://taskflow.inksphere.space")
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
var app = builder.Build();

// Middlewares

app.UseGlobalExceptionHandling();

app.UseRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    // The local LiveKit container cannot trust ASP.NET's self-signed certificate and some smoke-test
    // hosts have no development certificate. Keep the development-only Phase 0 probe group on HTTP;
    // every other request still redirects.
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/api/dev/meetings/livekit"),
        branch => branch.UseHttpsRedirection());
}
else
{
    app.UseHttpsRedirection();
}
app.UseCors("AngularPolicy");
app.UseAuthentication();
app.UseRateLimiter();

// After UseAuthentication so the admin bypass can read the role
// claim; before UseAuthorization so a held-off request never
// reaches a controller.
app.UseMaintenanceMode();

app.UseAuthorization();
app.UsePlannerObservability();

app.MapControllers();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapMeetingMediaProbe();
}

app.MapGet(
    "/health/ready",
    async (TaskFlowDbContext context, CancellationToken cancellationToken) =>
        await context.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

try
{
    if (!app.Environment.IsEnvironment("Testing"))
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();

        await context.Database.MigrateAsync();

        var passwordHasher =
            scope.ServiceProvider
                .GetRequiredService<IPasswordHasher>();

        var objectStorage =
            scope.ServiceProvider
                .GetRequiredService<IObjectStorage>();

        await objectStorage.EnsureBucketExistsAsync();

        // Roles first, because the user seeder assigns
        // the Admin role to the seeded admin user.
        await RoleSeeder.SeedAsync(context);

        await UserSeeder.SeedAsync(
            context,
            passwordHasher);

        // Organization permission catalog — populated from
        // OrganizationPermissionNames so roles can be granted
        // permissions by id.
        await OrganizationPermissionSeeder.SeedAsync(context);

        // Platform settings singleton. Inserts only when the table is
        // empty, so an admin's saved values survive every restart.
        await PlatformSettingSeeder.SeedAsync(context);
    }

    await app.RunAsync();
}
catch (Exception exception)
{
    var failure = ContainsException<AddressInUseException>(exception)
        ?
            "TaskFlow API could not start because its configured port is already in use. " +
            "Keep the existing API instance or stop it before launching another one."
        : ContainsException<OptionsValidationException>(exception)
            ? "TaskFlow API could not start because required configuration is missing or invalid."
            : "TaskFlow API stopped during startup. See the error below for details.";

    // Startup exceptions used to escape the process and could trigger a native
    // Windows 'unknown software exception (0xe0434352)' dialog. Log the failure
    // and return a non-zero exit code instead, so launchers can report it cleanly.
    app.Logger.LogCritical(exception, "{StartupFailure}", failure);
    Environment.ExitCode = 1;
}

static bool ContainsException<TException>(Exception exception)
    where TException : Exception
{
    for (var current = exception; current is not null; current = current.InnerException)
    {
        if (current is TException)
        {
            return true;
        }
    }

    return false;
}

public partial class Program;
