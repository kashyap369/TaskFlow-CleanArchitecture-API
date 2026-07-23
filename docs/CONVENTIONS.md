# TaskFlow — Coding Conventions

Derived from the existing code. When adding features, copy the shape of the nearest existing feature in the same module.

## Feature Folder Layout

```
TaskFlow.Application/Features/{Module}/{Entity}/Commands/{FeatureName}/
    {FeatureName}Command.cs          — sealed record, IRequest<TResponse>
    {FeatureName}CommandHandler.cs   — sealed class, IRequestHandler<,>
    {FeatureName}CommandValidator.cs — AbstractValidator<TCommand> (when input needs validation)
```

Modules: `Identity`, `Organizations`, `WorkManagement`. Entities: `User`, `Organization`, `OrganizationRole`, `OrganizationMember`, `OrganizationInvitation`, `Projects`, `Tasks`, `SubTasks`.

### Read side (Queries) — Dapper

Layout: `Features/{Module}/{Entity}/Queries/{Name}/{Name}Query.cs` holds **both** the query record (`IRequest<Dto>`) and its handler in one file (query records are tiny — this differs from commands, which stay split). DTOs live in `Features/{Module}/{Entity}/DTOs/Queries/` (often one `XxxDtos.cs` per entity).

Handler pattern:
- Inject `ISqlConnectionFactory` (+ `ICurrentUserService` for "my …" queries).
- `using var connection = _sqlConnectionFactory.Create();` then Dapper `QueryAsync` / `QuerySingleOrDefaultAsync` with a `CommandDefinition(sql, params, cancellationToken: ct)`.
- Raw SQL rules (Dapper bypasses EF): **quote every identifier** (`"Users"`, `"FirstName"` — Postgres folds unquoted names to lowercase), alias every column to the DTO property name, and **always** add `"IsDeleted" = FALSE` (the EF soft-delete filter does not apply). Enums map from their int columns automatically.
- Date params bound to `timestamptz` columns must be UTC-kind: `DateTime.SpecifyKind(x, DateTimeKind.Utc)`.
- Reports live under `Features/Reporting/`.

**Read-side authorization (IDOR guard).** A query that returns organization-scoped data must be access-checked. Do this by having the query record implement the matching marker interface from `Common/Authorization/AccessScopedRequests.cs` — `IOrganizationScopedRequest` (property `OrganizationId`), `IProjectScopedRequest` (`ProjectId`), `ITaskScopedRequest` (`TaskId`), `ITeamScopedRequest` (`TeamId`), `IRoleScopedRequest` (`OrganizationRoleId`), `IUserScopedRequest` (`UserId`), or `IMemberReportScopedRequest` (`UserId`). `AccessGuardBehavior` (a MediatR pipeline behavior) resolves the id to its organization and calls `IOrganizationAccessGuard`, throwing `ForbiddenException` if the current user is not the owner/active member. No handler code needed — just the marker. Current-user "my …" queries and the global permission catalog are intentionally unmarked. Commands are never marked; they enforce permissions in the handler via `IOrganizationPermissionChecker`.

DTOs: `Features/{Module}/{Entity}/DTOs/Commands/{FeatureName}/{FeatureName}ResponseDto.cs` — plain class with `init`/settable properties. Commands themselves are records and serve as the request DTO (bound directly from the body in controllers).

## Naming

| Thing | Pattern | Example |
|---|---|---|
| Command | `{Verb}{Entity}Command` | `CreateTaskCommand`, `RefreshUserTokenCommand` |
| Handler | `{Command}Handler` | `LoginUserCommandHandler` |
| Validator | `{Command}Validator` | `RegisterUserCommandValidator` |
| Response DTO | `{Feature}ResponseDto` | `LoginUserResponseDto` |
| Repository | `I{Entity}Repository` (Domain/Interfaces/{Module}/) → `{Entity}Repository` (Infra) | `ITaskRepository` → `TaskRepository` |
| Domain event | `{Entity}{PastTenseVerb}Event` | `UserRegisteredEvent` |
| Event handler | `{Event}Handler` implementing `IDomainEventHandler<T>` | `UserRegisteredEventHandler` |
| Error code | SCREAMING_SNAKE | `INVALID_CREDENTIALS`, `ORGANIZATION_ALREADY_EXISTS` |

## Style

- `sealed` on handlers, services, value objects, exceptions; `sealed record` for commands.
- Constructor injection with `private readonly` fields, assigned in a plain constructor (no primary constructors in existing code).
- Vertical formatting: one argument per line on multi-arg calls (existing code is very "tall" — match it).
- Entities: private setters, `protected` parameterless ctor for EF, guard clauses throwing `ArgumentException`, business methods that call `MarkAsUpdated()` and raise domain events. No public setters ever.
- Idempotent guards in entity methods (`if (IsRevoked) return;`).

## Controllers

- Route: `[Route("api/auth")]`-style literal or `[Route("api/[controller]")]`; `[ApiController]`; sealed where done already.
- Thin: inject `IMediator` only; body-bind the command; return `Ok(...)` for creates/reads, `NoContent()` for updates/deletes; pass `CancellationToken` through.
- Sub-actions as verbs in the route: `PUT api/task/{taskId:int}/start`, `/complete`.
- Response envelope `ApiResponse<T>` (see Api/Models/Responses) — Auth uses it; newer/other controllers should adopt it too when touched.
- Authorization: `[Authorize(Policy = AuthorizationPolicies.X)]` using constants, never inline strings. Every controller has `[Authorize]` (AllRoles by default; AdminOnly for admin-only actions); a new controller must add one. Auth endpoints (register/login/refresh/logout) stay anonymous.

## Handler Rules

- Load via repository → validate business rules (throw typed Application exceptions with error code) → mutate via entity methods → `repository.Add/Update` → `await _unitOfWork.SaveChangesAsync(ct)`.
- Current user always from `ICurrentUserService.UserId` — never from the request body.
- Return the entity Id (int) for creates, a ResponseDto for reads/auth, nothing (Unit) for updates/deletes.

## New Endpoint Checklist

1. Command record + handler (+ validator if the input can be malformed) in the right feature folder.
2. Business rules in the Domain entity (add a method; raise a domain event if significant).
3. Repository method if a new data access shape is needed (interface in Domain, impl in Infra).
4. Register any **new** repository or domain-event handler in `Infra/DependencyInjection/DependencyRegistration.cs`.
5. Controller action delegating to MediatR, with the right policy attribute (or the dev-stage comment).
6. New entity? Add DbSet + EF configuration class in `Infra/Persistence/Configurations/{Module}/`, then a migration.
