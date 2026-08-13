using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Extensions;
using TaskFlow.Api.Services;
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

// Services

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddHealthChecks();

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
app.UseHttpsRedirection();
app.UseCors("AngularPolicy");

app.UseAuthentication();

// After UseAuthentication so the admin bypass can read the role
// claim; before UseAuthorization so a held-off request never
// reaches a controller.
app.UseMaintenanceMode();

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.MapGet(
    "/health/ready",
    async (TaskFlowDbContext context, CancellationToken cancellationToken) =>
        await context.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

using (var scope = app.Services.CreateScope())
{
    var context =
        scope.ServiceProvider
            .GetRequiredService<TaskFlowDbContext>();

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

app.Run();
