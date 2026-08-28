# TaskFlow — Architecture

## Layers (Clean Architecture)

```
TaskFlow.Api  →  TaskFlow.Application  →  TaskFlow.Domain
TaskFlow.Infra implements Domain/Application contracts (referenced only by Api for DI)
```

Dependency rule: Domain depends on nothing; Application depends only on Domain; Infra depends on Application + Domain; Api depends on Application + Infra (composition root).

DI is composed via two extension methods called from `Program.cs`:
- `AddApplication()` ([Application/DependencyInjection/DependencyRegistration.cs](../TaskFlow.Application/DependencyInjection/DependencyRegistration.cs)) — MediatR, FluentValidation validators, and two pipeline behaviors in order: `ValidationBehavior` then `AccessGuardBehavior` (read-side authorization).
- `AddInfrastructure(config)` ([Infra/DependencyInjection/DependencyRegistration.cs](../TaskFlow.Infra/DependencyInjection/DependencyRegistration.cs)) — DbContext, repositories, UnitOfWork, JWT auth, email, domain event handlers, `ISqlConnectionFactory` (Dapper), `IOrganizationPermissionChecker`, `IOrganizationAccessGuard`. **Every new repository / domain-event-handler / security service must be registered here.**

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

## Read Path (Queries) — Dapper

```
Controller → MediatR → ValidationBehavior → AccessGuardBehavior (read authz)
  → QueryHandler → ISqlConnectionFactory.Create() → Dapper raw SQL → DTO
```

- `ISqlConnectionFactory` ([Infra/Dapper/SqlConnectionFactory.cs](../TaskFlow.Infra/Dapper/SqlConnectionFactory.cs)) hands out Npgsql connections. Query handlers use Dapper directly — no EF Core, no repositories on reads.
- Query record + handler live in one file under `Features/{Module}/{Entity}/Queries/{Name}/`; DTOs under `.../DTOs/Queries/`. Reports live under `Features/Reporting/`.
- Raw SQL rules (Dapper bypasses EF): quote every identifier (`"Users"`, `"FirstName"`), alias columns to DTO property names, always filter `"IsDeleted" = FALSE` (the EF soft-delete filter does not apply), and pass `timestamptz` params as `DateTime.SpecifyKind(x, DateTimeKind.Utc)`. See CONVENTIONS.md.
- **Read-side authorization**: a query that returns organization-scoped data implements a marker interface (`IOrganizationScopedRequest`, `IProjectScopedRequest`, `ITaskScopedRequest`, `ITeamScopedRequest`, `IRoleScopedRequest`, `IUserScopedRequest`, `IMemberReportScopedRequest` in `Application/Common/Authorization`). `AccessGuardBehavior` resolves the id to its org and calls `IOrganizationAccessGuard` (owner or active member), throwing 403 otherwise — closing IDOR. "My …" queries and the permission catalog are intentionally unmarked.

## Domain Layer

- `BaseEntity` — int Id + domain-event collection (`AddDomainEvent`).
- `AuditableEntity : BaseEntity` — CreatedAt/UpdatedAt/IsDeleted/DeletedAt, `MarkAsUpdated()`, `SoftDelete()`, `Restore()`. A global EF query filter (`TaskFlowDbContext.ApplySoftDeleteQueryFilter`) hides soft-deleted rows for all `AuditableEntity` types.
- Aggregate roots are marked with `IAggregateRoot` (marker interface): `User`, `SystemRole`, `Organization`, `Team`, `OrganizationPermission`, `TaskWorkLog`.
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
- **Controller authorization**: every controller carries `[Authorize(Policy = AllRoles)]` (any authenticated user); `UserController.GetAll` is `AdminOnly`; AuthController (register/login/refresh/logout) is anonymous. New controllers must add `[Authorize]`.
- **Three layers of authorization**, distinct and complementary:
  1. *System-role policies* (`[Authorize]` on controllers) — is the caller authenticated / an admin?
  2. *Org permissions* on write commands — `IOrganizationPermissionChecker` ([Infra/Security](../TaskFlow.Infra/Security/OrganizationPermissionChecker.cs)): owner bypasses, else the caller's org role must hold the named permission (`Domain/Constants/OrganizationPermissionNames`).
  3. *Read-side org scoping* on queries — `IOrganizationAccessGuard` via `AccessGuardBehavior` (see Read Path): the caller must own or actively belong to the org that owns the resource.

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

## Planner architecture

Planner Phases 17–23 are specified in [PLANNER.md](PLANNER.md). The binding architectural boundary is:
Excalidraw owns canvas presentation and interaction; TaskFlow owns projects, work items, requirements,
resources, authorization, progress, and history. An Excalidraw element links through a persisted
`PlannerNode` to a canonical TaskFlow entity/resource. Scene JSON is never a business-data or
authorization source of truth and never contains binary media.

Phase 18 implements creator-only personal-project boards with one primary `PlannerBoard` per project,
immutable `PlannerSceneRevision` checkpoints, and stable `PlannerNode` identities. Authorized scene
loads/saves use revision/ETag optimistic concurrency; the PostgreSQL revision uniqueness constraint is
also translated to a 409 so simultaneous writers cannot become a 500 or silently overwrite. Scene JSON
is UTF-8-size limited and excludes binary media. IndexedDB is an ordered recovery cache, never the
authority. Phase 21 places binary resources behind `IObjectStorage`; PostgreSQL contains metadata only.

Phase 19 makes `PlannerNode` the stable UUID bridge from an Excalidraw element id to exactly one
canonical Project, Task, or Subtask integer id. Planner-aware commands create the work record and node
in one EF transaction; business fields never come from scene JSON. The workspace query joins the live
aggregate state into backend-derived counts, completion, dates, status, and planning fields. Removing a
node is explicitly either unlink-only or canonical deletion; deleting the owning project remains in the
normal Projects flow. Unique filtered indexes prevent duplicate entity links within a board.

Phase 20 adds platform-owned `PlannerTemplate` definitions and append-only `PlannerTemplateVersion`
snapshots. AdminOnly commands own Draft/Published/Archived transitions and validate JSON fields/defaults
against the five fixed object contracts; templates cannot execute code or create schemas. Member reads
return only published active definitions. Each new `PlannerNode` may reference one published version,
so presentation/default changes never rewrite existing plans and archived versions remain renderable.

Phase 21 adds `PlannerResource` for note/link/document metadata and `PlannerAsset` for private stored-
object metadata. Resource nodes target exactly one resource UUID; binary content never enters PostgreSQL
or Excalidraw JSON. Every resource route first authorizes the creator-owned project. Uploads enforce a
25 MB limit, extension/content-type allowlist, safe filename, SHA-256 checksum, and scan status through
the replaceable `IPlannerAssetScanner` hook; preview/download streams only after a Clean result. Unlinking
a node retains its resource and object for the project library, while explicit deletion removes database
metadata and the object. Soft-deleting a project retains its private objects for recovery; permanent
storage lifecycle cleanup is an operational hardening concern for Phase 23.

Phase 22 adds immutable `RequirementBaseline` aggregates with ordered `RequirementSnapshot` children
and append-only `RequirementChange` audit rows. Finalization captures project/task/subtask scope in one
transaction. Requirement-bearing Project, Task, and SubTask changes are detected inside
`TaskFlowDbContext.SaveChangesAsync`, after an active baseline lookup and before commit, so mutations
through non-Planner controllers cannot bypass history. Scope fields are serialized as stable JSON;
execution-only status/completion/time-log fields are deliberately excluded. The save plus audit rows
share one database transaction, and actor identity comes from the authenticated request. Comparison
resolves the latest state against the immutable snapshot and derives effective New/Changed/Removed
state, including reverted-change suppression and optional user-supplied reasons.

### Phase 23 operational boundary

Planner scene persistence reads the board root without hydrating its node/resource graph, validates a
maximum 5 MB UTF-8 document with at most 5,000 elements, and keeps a rolling 100-revision history backed
by a `(BoardId, CreatedAt)` index. Scene JSON cannot embed binary data or unsafe URL schemes. Uploaded
assets additionally require matching file signatures and pass the scanner gate before private streaming.

`Planner:Enabled` is the server rollback switch; clients have a matching build-time feature flag. The
Planner middleware emits activities, duration/request/failure/conflict/mutation metrics, slow/error logs,
and actor/trace mutation audit events without recording scene/file content. Separate Planner and upload
rate limits constrain abuse. Disabling Planner hides client routes/navigation and makes server routes
unavailable while preserving all database state and any legacy browser scene retained for rollback.

## Known Placeholders / Loose Ends
- `Domain/Common/Result.cs` — empty stub (Result pattern not adopted; exceptions used instead)
- `Domain/Exceptions/BadRequestException.cs` — defined but the middleware handles Application-layer exceptions; prefer those
- Email verification: `User.VerifyEmail()` exists but no endpoint/token flow yet (seeder calls it directly)
- List queries are unpaginated/unfiltered; `ApiResponse<T>` envelope is only used by AuthController — both flagged in PHASES.md
- Automated coverage now includes Planner domain/application tests and real HTTP/PostgreSQL migration,
  ownership, restore, and concurrency integration tests; broader legacy API coverage remains backlog.
