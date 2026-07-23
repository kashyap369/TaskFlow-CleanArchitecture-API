using TaskFlow.Api.Extensions;
using TaskFlow.Api.Services;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.DependencyInjection;
using TaskFlow.Infra.DependencyInjection;
using TaskFlow.Infra.Persistence.Context;
using TaskFlow.Infra.Seeder.Identity.Role;
using TaskFlow.Infra.Seeder.Identity.User;
using TaskFlow.Infra.Seeder.Organization.Permission;

var builder = WebApplication.CreateBuilder(args);

// Services

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
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

app.UseHttpsRedirection();
app.UseCors("AngularPolicy");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context =
        scope.ServiceProvider
            .GetRequiredService<TaskFlowDbContext>();

    var passwordHasher =
        scope.ServiceProvider
            .GetRequiredService<IPasswordHasher>();

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
}

app.Run();