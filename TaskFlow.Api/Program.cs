using TaskFlow.Api.Extensions;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.DependencyInjection;
using TaskFlow.Infra.DependencyInjection;
using TaskFlow.Infra.Persistence.Context;
using TaskFlow.Infra.Seeder.Identity.User;

var builder = WebApplication.CreateBuilder(args);

// Services

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

builder.Services.AddApplication();

builder.Services.AddInfrastructure(
    builder.Configuration);

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

    await UserSeeder.SeedAsync(
        context,
        passwordHasher);
}

app.Run();