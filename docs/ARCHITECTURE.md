# TaskFlow — Architecture

## Layers (Clean Architecture)

```
TaskFlow.Api  →  TaskFlow.Application  →  TaskFlow.Domain
TaskFlow.Infra implements Domain/Application contracts (referenced only by Api for DI)
```

Dependency rule: Domain depends on nothing; Application depends only on Domain; Infra depends on Application + Domain; Api depends on Application + Infra (composition root).

DI is composed via two extension methods called from `Program.cs`:
- `AddApplication()` ([Application/DependencyInjection/DependencyRegistration.cs](../TaskFlow.Application/DependencyInjection/DependencyRegistration.cs)) — MediatR, FluentValidation validators, `ValidationBehavior` pipeline
- `AddInfrastructure(config)` ([Infra/DependencyInjection/DependencyRegistration.cs](../TaskFlow.Infra/DependencyInjection/DependencyRegistration.cs)) — DbContext, repositories, UnitOfWork, JWT auth, email, domain event handlers. **Every new repository/domain-event-handler must be registered here.**

## Write Path (Commands)

```
Controller → MediatR → ValidationBehavior (FluentValidation) → CommandHandler
  → Domain entity method (business rules, raises domain events)
  → Repository (EF Core) → IUnitOfWork.SaveChangesAsync()
  → DbContext dispatches domain events after save → domain event handlers (e.g. send email)
```

- Repositories are **write-side only**: load aggregate, Add/Update/Remove. They live per-module under `Infra/Persistence/Repositories/`; interfaces in `Domain/Interfaces/`.
- `Remove()` performs a **soft delete** via `AuditableEntity.SoftDelete()`.
- Handlers depend on interfaces only; `IUnitOfWork.SaveChangesAsync()` commits.

## Read Path (Queries) — planned, not implemented yet

Intended design: `Query → Dapper → raw SQL → DTO` (no EF Core, no repositories).
Current state: `Infra/Dapper/DapperContext.cs` is an empty placeholder; no Queries folders exist anywhere. When building reads, create `Features/{Module}/{Entity}/Queries/{Name}/` mirroring the Commands layout.

## Domain Layer

- `BaseEntity` — int Id + domain-event collection (`AddDomainEvent`).
- `AuditableEntity : BaseEntity` — CreatedAt/UpdatedAt/IsDeleted/DeletedAt, `MarkAsUpdated()`, `SoftDelete()`, `Restore()`. A global EF query filter (`TaskFlowDbContext.ApplySoftDeleteQueryFilter`) hides soft-deleted rows for all `AuditableEntity` types.
- Aggregate roots are marked with `IAggregateRoot` (marker interface): `User`, `SystemRole`, `Organization`.
- Entities are behavior-first: private setters, protected parameterless ctor (for EF), guard clauses in ctor, named business methods (`User.Register()`, `Task.Start()`, `Organization.Suspend()`).
- Value objects: `Email` (normalized lowercase, validated), `PhoneNumber` (10–15 digits), `FullName`. Stored as owned values; repositories compare via `x.Email.Value == email.Value`.

## Domain Events

- Events defined in `Domain/DomainEvents/` (e.g. `UserRegisteredEvent`); raised inside entity methods.
- Handlers implement `IDomainEventHandler<TEvent>` (Application layer) and are registered in Infra DI.
- Dispatch: `TaskFlowDbContext.SaveChangesAsync` collects events from tracked entities, saves, **then** dispatches via `DomainEventDispatcher` (reflection-based, in-process, synchronous), then clears events. Note: handlers run after the save but in the same request — a failing handler (e.g. SMTP down) throws after data is already persisted.
- Example flow: `User.Register()` → `UserRegisteredEvent` → `UserRegisteredEventHandler` → welcome email from `Infra/Email/Templates/Welcome.html`.

## Authentication & Authorization

- JWT Bearer (HMAC-SHA256), settings in `JwtSettings` config section (Issuer, Audience, SecretKey, ExpiryMinutes, RefreshTokenExpiryDays). ClockSkew = 0.
- Claims: NameIdentifier (user id), Email, one Role claim per system role.
- **Refresh tokens**: random 64-byte strings stored in DB (`RefreshToken` entity, tracks IP, revocation, replacement chain). Rotation on every refresh; **reuse detection** revokes all active tokens for the user (`RefreshUserTokenCommandHandler`).
- `ICurrentUserService` (implemented in Api) exposes `UserId`, `Email`, `IpAddress` from the JWT/connection — handlers use this, never request-body IDs.
- Policies in `Api/Constants/AuthorizationPolicies` + `Api/Extensions/ServiceCollectionExtensions.AddAuthorizationPolicies()`: `AdminOnly`, `ManagerAndAbove`, `AllRoles`, mapping to system roles Admin/Manager/User (`Domain/Constants/SystemRoleNames`).
- Login flow enforces user status (PendingVerification / Suspended / Inactive blocked with specific error codes).
- ⚠️ Organization and WorkManagement controllers are currently **unauthenticated by design** (dev stage) — each carries a comment with the intended `[Authorize(Policy = ...)]`.

## API Surface & Error Handling

- Success envelope: `ApiResponse<T>` { Success, Message, Data, Timestamp } — used by AuthController; other controllers still return raw values (inconsistent, to be unified).
- Errors: `ExceptionHandlingMiddleware` maps typed exceptions → HTTP status + `ApiErrorResponse` { Code, Message, FailureReason, Errors, TraceId }:
  - FluentValidation `ValidationException` → 400 with field errors
  - `NotFoundException` → 404, `ConflictException` → 409, `UnauthorizedException` → 401, `ForbiddenException` → 403, `BusinessException` → 400, anything else → 500
- `RequestLoggingMiddleware` logs method/path/status/duration/IP/user-agent with TraceId.
- CORS policy "AngularPolicy" allows http://localhost:4200.

## Persistence

- PostgreSQL via Npgsql; EF Core configurations per entity in `Infra/Persistence/Configurations/{Module}/`, applied by assembly scan.
- Migrations in `Infra/Migrations`; startup does **not** auto-migrate (seeders run on startup and assume the schema exists).
- Seeders (`Infra/Seeder/`): RoleSeeder (Admin/Manager/User system roles) then UserSeeder (admin@taskflow.com / Admin@123, verified, Admin role) — run from Program.cs on every startup, idempotent.

## Known Placeholders / Loose Ends
- `Infra/Dapper/DapperContext.cs` — empty stub (read side not built)
- `Domain/Common/Result.cs` — empty stub (Result pattern not adopted; exceptions used instead)
- `Domain/Exceptions/BadRequestException.cs` — defined but the middleware handles Application-layer exceptions; prefer those
- Email verification: `User.VerifyEmail()` exists but no endpoint/token flow yet (seeder calls it directly)
