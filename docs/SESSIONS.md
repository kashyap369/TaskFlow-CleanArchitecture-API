# TaskFlow — Session Log

> Append-only. 3–5 lines per session. Focus on gotchas, dead ends, and decisions — things git history doesn't capture.
>
> **▶ Next session: the BACKEND IS FEATURE-COMPLETE** (**82 endpoints**). Phases 10–13 closed every
> §3.0 vision gap and every open defect (§4.2/§4.3/§4.3b/§4.4). Organization, Reporting, Admin and
> Individual are all at **100%** on the API. **Remaining work is entirely frontend** — see the client's
> `docs/PHASES.md` Phases 24–29. Backend backlog left (non-blocking): automated tests, pagination,
> `ApiResponse<T>` consistency, forgot-password, Docker/CI.
> Cross-project status: [ProjectCompletion.md](ProjectCompletion.md).

## 2026-08-15 (Private personal projects + Docker-free development)

- Added nullable project organization ownership with a creator/title partial unique index and two
  personal-project endpoints. Project access now branches cleanly: organization membership/permission
  for organization projects, exact creator match for personal projects.
- Personal task creation accepts an optional project only when both are organization-free and owned by
  the caller. The existing task-scoped guard keeps subtasks and work logs private as well.
- Applied `AddPrivatePersonalProjects` to the configured development database. Live project → task →
  subtask tests passed; a second valid user and an admin both received 403 across ownership boundaries.
- Added a development-only local filesystem object-storage provider. Production continues to select S3;
  local API + Angular now run directly without Docker.

## 2026-08-15 (Project authorization + Individual organization access)
- Project create/update/delete handlers were the exception to the command-side authorization rule:
  they accepted every authenticated system role and never checked organization membership or the
  project permission catalog. Creation now requires `CreateProject`; update/delete require
  `ManageProjects`, with the standard owner bypass.
- Individual accounts already became real organization members after accepting an invitation, but
  the Angular portal guard treated account type as exclusive and made those memberships unusable.
  Individual accounts now retain `/member` as home while being allowed into joined `/organization`
  workspaces, with explicit switches in both layouts and membership refresh after acceptance.
- Backend build, frontend lint/build, and all 16 focused browser regressions pass. A broader run
  passed 215/215 specs after excluding the four-test public-header spec whose real navigation
  deliberately triggers Karma's existing full-page-reload disconnect.

## 2026-08-14 (Account recovery and passwordless login)
- Added persisted, purpose-scoped one-time codes for password reset and email-code login. Only an
  HMAC-SHA256 digest is stored; comparisons are fixed-time; codes expire in 10 minutes, are single
  use, lock after five failures, and enforce a 60-second resend cooldown.
- Request endpoints always return the same response for unknown, ineligible, and eligible accounts;
  SMTP failures are logged but do not become an account-enumeration signal. Four auth endpoints also
  have a shared per-IP rate-limit policy.
- Both login methods now share `IAuthSessionIssuer`, so JWT claims, refresh tokens, login audit time,
  and role behavior cannot drift. Password reset revokes every active refresh token.
- Migration `AddOneTimeCodes` was generated and applied locally. Build succeeds; pre-existing nullable
  warnings remain. Local API boot is blocked by missing ObjectStorage development configuration.

## 2026-07-26 (Phases 10–13 — Organization / Reporting / Admin to 100%)
- **Fixing one security hole uncovered a bigger one.** §4.3 said org update/delete had no
  authorization. While adding the owner check I looked at the neighbouring member commands and found
  **all four** (`Remove`/`Deactivate`/`Activate`/`ChangeMemberRole`) enforced *nothing* — any
  authenticated user could deactivate or remove any member of any org. It was live: the seeded admin,
  who belongs to no organization, could act on org 2's members. Logged as §4.3b. **Third time this
  family has appeared** (§4.1b in Phase 9 was the first). The tell is structural: when a command
  handler doesn't take `IOrganizationAccessGuard` or `IOrganizationPermissionChecker` in its
  constructor, it almost certainly enforces nothing — grep constructors, not method bodies.
- **`ChangeMemberRole` validated that the role existed but not that it belonged to the same
  organization.** A role id from another org was accepted, silently importing that org's permission
  set. Existence checks are not scope checks.
- **Owner-only needed a new guard method.** `EnsureOrganizationAsync` permits owner *or active member*
  — right for reading tasks, far too weak for renaming or deleting an entire workspace. Added
  `EnsureOrganizationOwnerAsync` rather than reusing the loose one.
- **The admin bypass is deliberately narrow.** §4.2 is fixed by short-circuiting `EnsureUserAsync` for
  a platform admin — *user profiles only*. It would have been one line to bypass
  `EnsureOrganizationAsync` too and turn the admin role into a skeleton key over every org's data.
  Didn't. `GET /user` was already AdminOnly, so this only makes the detail agree with the list.
- **Team assignment is its own route, not a field on `UpdateTaskCommand`.** This project has already
  been bitten twice by "the list DTO lacks the field, so saving the edit form blanks it" (task
  description, organization description). `PUT /task/{id}/team/{teamId}` + `DELETE /task/{id}/team`
  cannot be triggered by accident, and it matches how assign/unassign already work.
- **`TaskListSql` needed a LEFT JOIN, not a join.** `TeamId` is optional, so an inner join would have
  silently dropped every task without a team — which, before this phase, was all of them.
- **A setting nothing reads is worse than no setting**, because the UI implies it works. So
  `RegistrationOpen` is enforced in `RegisterUserCommandHandler` and `MaintenanceMode` in real
  middleware. Both **fail open** when the settings row is missing, matching pre-existing behaviour.
- **Maintenance mode needs escape hatches or it's a footgun.** `/api/auth/*` stays open and admins pass
  through everything — otherwise an admin flips the switch and locks themselves out of the very screen
  that turns it off. Both hatches verified live before trusting the feature.
- **`$pid` is read-only in PowerShell.** A verification script silently sent the *shell's* PID as a
  task id and produced two baffling 404s. Not an API bug — but worth knowing before debugging one.

## 2026-07-26 (Phase 9 completed — verification, reopen, and a NOT NULL bug the UI found)
- **The workspace worked but the *account lifecycle* didn't.** Auditing against OVERVIEW turned up two
  gaps nobody had listed: a completed task **couldn't be reopened** (`SubTask.Reopen()` existed,
  `Task` had none) and a **newly registered account could never sign in** — PendingVerification with no
  verification endpoint anywhere. The second one meant the Individual account was unreachable for a
  real user no matter how good the workspace was. Audit the promises, not just the endpoints.
- **Email verification with no schema change.** Used a **stateless HMAC token**
  (`userId.expiry.signature`, keyed with the JWT secret, 48h) instead of a token column — no migration,
  can't be forged, expires itself, and verifying twice is a no-op because `User.VerifyEmail()` already
  returns early. `UserRegisteredEventHandler` resolves the user **by email**: the event is raised inside
  `Register()` before the row exists, so it can't carry the id. Resend always returns 200 — replying
  "no such user" would make it an account-enumeration oracle.
- **🚨 The new UI immediately found a real bug: `TaskWorkLogs.Notes` was NOT NULL** while the domain
  wrote `notes?.Trim()`. Starting a timer without a note **500'd**. The org portal never hit it because
  its form always sent a string. **A non-nullable CLR `string` silently becomes a NOT NULL column** —
  exactly the earlier `RefreshToken.RevokedByIp` bug. Fixed: `string?` + `IsRequired(false)` + migration
  `MakeWorkLogNotesNullable`. Worth grepping the remaining entities for the same shape.
- **Reopen defers to the subtasks.** `Task.Reopen()` clears `ActualCompletionDate`, then calls
  `RecalculateStatus()` if the task has subtasks rather than forcing Todo — otherwise a task whose
  subtasks are all complete would flip to Todo and immediately disagree with its own children.
- Verified live end to end, twice: through the API, then through the browser as a **brand-new account
  created via the real sign-up form**.

## 2026-07-26 (Phase 9 SHIPPED — Individual account: personal workspace)
- **The whole feature was one nullable parameter plus a security fix.** `CreateTaskCommand.OrganizationId`
  `int` → `int?` was the only functional blocker; Domain, the DB column, `GetByTitleAsync`,
  `EnsureTaskAsync` and the read queries were already written for personal tasks. **No migration, no
  domain change, no schema change.** Survey the full stack before estimating.
- **9.0 first, and it earned its place.** 11 command handlers (task ×4, subtask ×5, work-log ×2) had
  **zero** authorization; `CreateTask` didn't even check you belonged to the org you were creating in.
  Proved it live: as a user in no organization, start/complete/delete/subtask/worklog/update against
  another org's task now all return **403** — every one succeeded before. Had personal tasks shipped
  first, that hole would have covered private data.
- **Reused `EnsureTaskAsync` rather than writing a write-side guard.** It already encoded exactly the
  right rule (org task → owner/active member; personal task → creator only). Called it directly in the
  handlers instead of marking commands for `AccessGuardBehavior`, keeping the documented "commands
  enforce their own permissions" convention and leaving the behavior read-only.
- **Two routes, one command.** `POST /task` now 400s (`ORGANIZATION_ID_REQUIRED`) instead of silently
  creating a personal task when the client omits the org id — that would have been invisible data
  corruption. `POST /task/personal` takes a request record with no OrganizationId/ProjectId *by
  construction*, so the trap can't be reintroduced.
- **Gotcha for the frontend:** `GET /worklog/mine` **requires** `?from&to`. Omitting them binds
  `0001-01-01` and returns `[]` rather than erroring — it looks like "no data" when it's "no window".
  Same for `/report/me`. Cost me a confused minute during verification.
- Verified end-to-end then **restored seed state** (org 2 back to its 2 tasks, all test rows deleted).

## 2026-07-26 (Planned Phase 9 — Individual account; audited OVERVIEW as a spec)
- **`OVERVIEW.md` was claiming a vision that isn't built.** It said the vision was "implemented and
  verified end-to-end" and listed "personal tasks (nullable org)" as done. Audited it line by line:
  **Organization ~85%, Reporting ~70%, Individual ~0%.** Corrected the file. An OVERVIEW that overstates
  is worse than none — it's the doc a new session reads first to decide what's left.
- **Surveyed before planning, and the survey changed the plan's size.** Domain (`OrganizationId` is
  `int?`, `IsPersonal`, `Assign()` refuses personal), the **DB column (already nullable — no migration)**,
  `TaskRepository.GetByTitleAsync` (already branches on null org), `EnsureTaskAsync` (personal → creator
  only) and the read queries are **all already built**. The Individual account is blocked by exactly one
  thing: `CreateTaskCommand.OrganizationId` being non-nullable. Trace the full stack before estimating —
  this looked like a feature and is closer to a parameter change.
- **🚨 Found a bigger problem while surveying:** `DeleteTask`, `StartTask`, `CompleteTask`,
  `CreateSubTask` and `StartWorkLog` handlers enforce **nothing at all** — no ownership, no org check, no
  permission. Any authenticated user can delete any task by id. `AccessGuardBehavior` doesn't cover it
  because it only inspects *reads*; the "commands enforce their own permissions" convention held for the
  org-permission-gated handlers but was never applied to these. Scheduled as **Phase 9.0, ahead of the
  feature** — shipping personal tasks onto an unguarded write side would mean private data anyone can
  mutate.
- **Decision: two routes, one command.** `POST /task` keeps requiring an org (400 without); a new
  `POST /task/personal` rejects org/project. A single nullable field would let a client bug that drops
  `organizationId` silently create a *personal* task instead of failing — invisible data corruption.
- Cross-project status now lives in **[ProjectCompletion.md](ProjectCompletion.md)** (API ⇄ UI parity
  ledger); update it whenever the API surface changes.

## 2026-07-23 (IDOR fix — read-side org scoping)
- Closed the IDOR gap: any authenticated user could read another org's data by guessing ids. Added `IOrganizationAccessGuard` (Infra/EF: owner or active member; resolves project/task/team/role → org; personal task → creator; user profile → self/shared-org; member report → self/owner-of-shared-org) and a MediatR `AccessGuardBehavior` that runs the check when a query implements one of the marker interfaces in `Common/Authorization/AccessScopedRequests.cs`.
- Chose the pipeline-behavior + marker-interface approach over editing 19 handlers: one line per query record, no handler changes, and it can't be forgotten as easily. Commands are NOT marked (they already enforce permissions).
- Verified live with a second user: seeded/verified `jane@example.com` via a throwaway Npgsql console (psql isn't installed; `dotnet ef` can't run arbitrary SQL). Result — admin (owner) 200 on org 1; jane (non-member) 403 on org/dashboard/tasks/team/role; jane self-profile 200 but admin-profile 403; jane "my" queries 200.
- Note: querying a non-existent org id now returns 403 (guard denies before the handler's 404) — intentional, avoids leaking existence. DB now contains test data (org "Acme Inc", a team, role, task; jane as a verified user).

## 2026-07-23 (Security pass — [Authorize])
- Applied `[Authorize(Policy = AllRoles)]` to every controller (org/work/team/worklog/report/user); `AdminOnly` on `UserController.GetAll`; AuthController left anonymous (register/login/refresh/logout — logout only needs the refresh token and `IpAddress` never throws). Chose AllRoles (not ManagerAndAbove as the old comments suggested) because an org owner may hold only the "User" system role — real org authz lives in the handlers via `IOrganizationPermissionChecker`.
- Verified: unauth → 401 on query/command/report endpoints; login open; admin reaches `/user/me` + AdminOnly list. Couldn't exercise the non-admin→403 path live (no verified non-admin user; psql not installed) — it's standard `RequireRole` behavior.
- **Flagged the next security item in PHASES.md: read-side org scoping / IDOR** — read queries taking an orgId/projectId don't verify the caller's membership yet, so any authenticated user can read another org's data by guessing IDs.

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
