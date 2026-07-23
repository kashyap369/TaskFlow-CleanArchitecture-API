# TaskFlow — Session Log

> Append-only. 3–5 lines per session. Focus on gotchas, dead ends, and decisions — things git history doesn't capture.

## 2026-07-23 (Application layer — write + read)
- Built the full Application layer for the vision. Write side: account type in registration; team commands; task assign/unassign (raise domain events); role grant/revoke permission; work log start/stop/manual/delete; `OrganizationMemberInvitedEventHandler` + Invitation.html template. New `IOrganizationPermissionChecker` (Infra, EF-based): owner bypasses, else active member's role must hold the permission — used by all org-permission-gated handlers.
- Read side (first queries in the project): chose Dapper-in-Application with `ISqlConnectionFactory` (Infra `SqlConnectionFactory` via Npgsql; replaced the empty `DapperContext` stub; added Dapper pkg to Application). Convention: query record + handler in ONE file under `Queries/{Name}/`, DTOs shared per entity. ~25 queries + 4 reports.
- Dapper gotchas handled: quote all PG identifiers, alias columns to DTO props, add `"IsDeleted"=FALSE` everywhere (EF filter doesn't apply), enums map from int, and `DateTime.SpecifyKind(...Utc)` before binding to `timestamptz` params (else Npgsql throws).
- **Pre-existing bug found during verification:** `RefreshToken.RevokedByIp`/`ReplacedByToken` were non-nullable strings (→ NOT NULL) but only set on revoke, so login's refresh-token insert 500'd every time. Made them `string?` + `IsRequired(false)`; migration `MakeRefreshTokenRevocationNullable`. Login works now.
- Verified live: login/JWT, profile, register w/ account type, org create + mine, team create (owner bypass), grant permission + role detail, task create, dashboard aggregate. `dotnet-ef` on PATH at `~/.dotnet/tools`; app on http://localhost:5138 (needs `ASPNETCORE_ENVIRONMENT=Development`). To run the app you must stop it before `dotnet build` (file locks on Windows): `taskkill //F //IM dotnet.exe`.

## 2026-07-23 (Infra layer)
- Implemented Infra for the new domain: `TeamRepository` (Include members), `OrganizationPermissionRepository`, `TaskWorkLogRepository` (running-timer lookup via `EndedAt == null`) — all matching existing repo style. Registered all three in `DependencyRegistration`.
- Added `OrganizationPermissionSeeder` (idempotent, syncs catalog from `OrganizationPermissionNames.All`), wired into `Program.cs` after RoleSeeder/UserSeeder.
- No new migration needed — `DomainVisionFoundation` (last session) already covered the schema; DB was already up to date. Installed `dotnet-ef` global tool is on PATH at `~/.dotnet/tools`.
- Verified end-to-end: app boots on localhost:5138, seeder inserts all 9 permissions, no errors. `OrganizationRolePermission` has no dedicated repo by design — it's owned by `OrganizationRole` (persisted via the aggregate; load with `GetByIdWithPermissionsAsync`).
- Note for next session: `dotnet run` from Api needs `ASPNETCORE_ENVIRONMENT=Development` for Swagger; app listens on http://localhost:5138.

## 2026-07-23
- Set up Claude Code documentation structure: CLAUDE.md + docs/ (OVERVIEW, ARCHITECTURE, CONVENTIONS, PHASES, SESSIONS).
- Analyzed full codebase and filled in all docs from actual code.
- Owner defined the product vision: two account types (Individual / Organization — enum already existed unused), org teams, permission-based roles, task assignment, projects, and a reporting dashboard as the headline feature. OVERVIEW.md rewritten; PHASES.md now has an 8-phase roadmap (account types → Dapper read side → teams/assignment → permissions → time tracking → reporting).
- Built the Domain layer for the vision (see PHASES.md status). Design decisions: personal task = `Task.OrganizationId == null`, duplicate titles scoped per-org for org tasks and per-creator for personal ones (`GetByTitleAsync` signature changed); team removal deactivates membership instead of deleting so reports keep history; permissions modeled as a global catalog table + role→permission join, granted via `OrganizationRole.GrantPermission`; `TaskWorkLog` supports both a live timer (StartNew/Stop) and manual after-the-fact entries. `User.Register` takes `AccountType` with Individual default so existing callers/rows stay valid.
- Installed `dotnet-ef` global tool (wasn't on this machine). Migration `DomainVisionFoundation` generated but **not applied** — run `dotnet ef database update --project TaskFlow.Infra --startup-project TaskFlow.Api`.
- New repo interfaces (ITeamRepository, IOrganizationPermissionRepository, ITaskWorkLogRepository) have no Infra implementations/DI registrations yet — that's Application/Infra phase work.
- Gotchas found: `DapperContext` and `Domain/Common/Result.cs` are empty stubs; no Queries exist yet (write side only); Org/WorkManagement controllers deliberately unauthenticated (dev stage); domain events dispatch synchronously after SaveChanges (SMTP failure throws after data persisted); response envelope `ApiResponse<T>` only used by AuthController.
