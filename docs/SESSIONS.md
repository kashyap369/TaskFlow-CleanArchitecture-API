# TaskFlow — Session Log

## 2026-08-29 (Organization Calendar Phase 4)

- Added one organization-owned calendar aggregate for events, member leave and holidays, the
  `ManageCalendar` catalog permission, focused CRUD/window CQRS handlers and an additive migration.
- Defined the recurrence contract as None/Daily/Weekly/Monthly, interval 1–30 and optional inclusive
  end date. Reads expand occurrences inside a validated 366-day organization-scoped query window.
- Backend is 40/40 with real HTTP/PostgreSQL recurrence, all-day, organization-isolation and delete
  coverage; Release build and EF model-drift checks pass. Angular consumes all four new routes.

## 2026-08-29 (Organization Calendar Phase 3)

- Added `EstimateMinutes` / `WeeklyCapacityMinutes`, the additive capacity migration, and focused
  permission-gated writes without expanding the general task/member update contracts.
- Added a date-only, Monday-based Dapper query whose totals and workload state are computed in
  PostgreSQL. The test pass caught and fixed Dapper's unsupported DateOnly parameter binding while
  preserving DateOnly at the HTTP contract.
- Backend is 35/35 with real HTTP/PostgreSQL coverage for totals, UTC week edges, missing data and
  organization isolation; build and EF model-drift checks pass.

## 2026-08-29 (Organization Calendar Phase 2)

- Added the focused task schedule route/command/domain method instead of expanding general task update.
- Reused `ManageTasks`, rejected personal scheduling and validated target date >= start date; no schema
  or migration was needed.
- Added three scheduling tests; the full suite passes 30/30 when the existing one-time-code test secret
  is provided. Angular consumes the route with permission-aware drag/resize and failure rollback.

## 2026-08-28 (Planner Phase 23 — hardening and production rollout)

- Bounded scene size/depth/elements/strings and link schemes, optimized scene persistence, retained the
  latest 100 revisions, indexed revision reads, and validated upload signatures before scanning.
- Added server and client feature flags, Planner rate limits, traces/metrics, structured slow/error and
  mutation audit logs, private download headers, and explicit legacy browser-scene import with rollback.
- Backend is 27/27 and frontend is 240/240; builds, lint/design lint, Storybook, EF drift, large-scene
  performance, ownership/security integration coverage, and critical browser specs pass.
- Both production services were released through Dokploy; migration and live health evidence are tracked
  in ProjectCompletion.md.

## 2026-08-28 (Planner Phase 22 — immutable primary requirements and comparison)

- Added transactional Baseline 1 snapshots and persistence-boundary New/Changed/Removed auditing so
  normal API mutations cannot bypass requirement history; progress-only updates remain excluded.
- Added owner-authorized baseline/history/comparison routes plus Angular finalization, filters, reasons,
  immutable snapshot inspection, and field-level baseline/current differences.
- Real PostgreSQL HTTP coverage proves immutability, ownership, progress separation, additions, edits,
  removals, and filters. Backend is 22/22 and frontend is 236/236; builds/lint and EF drift pass.
- Phase 23 hardening, performance, observability, feature-flag rollout, and critical E2E flows are next.

## 2026-08-28 (Planner Phase 21 — notes, documents, and secure media)

- Added owner-scoped `PlannerResource`/`PlannerAsset` persistence and eight resource/file routes with
  private object storage, 25 MB/type limits, safe names, SHA-256, and a scanner extension point.
- Added Note/Link/Document canvas cards, creation and inspector flows, authorized preview/download,
  rename, unlink/relink retention, and explicit resource/object deletion; scene JSON stays binary-free.
- Added migration, domain tests, and disposable-PostgreSQL HTTP coverage. Backend is 21/21 and frontend
  is 234/234, with production build and EF model-drift checks green. Phase 22 is next.

> Append-only. 3–5 lines per session. Focus on gotchas, dead ends, and decisions — things git history doesn't capture.
>
> **Planner roadmap complete:** Phases 17–23 are delivered. Continue to treat [PLANNER.md](PLANNER.md)
> as the product/architecture contract and [ProjectCompletion.md](ProjectCompletion.md) as the release ledger.

## 2026-08-28 (Planner Phase 20 — admin-managed template library)

- Added fixed Project/Task/Subtask/Note/Document template contracts, Draft/Published/Archived lifecycle,
  immutable published versions, type-safe JSON fields/defaults, node snapshots, and migration.
- Added AdminOnly management/publication routes, member published-active reads, an admin library page,
  and a Planner picker that applies defaults and visual dimensions/colors to new linked cards.
- Archived templates disappear from the member picker while old nodes retain their version; Note and
  Document definitions remain visible but await Phase 21 resources. All verification gates pass.

## 2026-08-28 (Planner Phase 19 — linked work objects and live progress)

- Added stable PlannerNode links to canonical personal Project/Task/Subtask records and six
  owner-authorized workspace/node routes for atomic creation, editing, rehydration, unlinking, and deletion.
- Extended projects with problem statement, budget/currency, and approximate duration through
  `AddPlannerLinkedWorkItems`, preserving existing project-client update behavior.
- Added linked-card creation and automatic missing-card recovery, live backend-derived canvas labels,
  inspector editing, progress/counts, and explicit unlink-versus-delete actions in Angular.
- PostgreSQL HTTP integration proves cross-user denial, exact-once canonical creation, external status
  refresh, planning fields, and removal semantics. Backend tests are 15/15 and frontend tests are
  231/231; build, lint/design lint, Storybook, Impeccable detector, and EF model checks pass.

## 2026-08-28 (Planner Phase 18 — cloud persistence and concurrency)

- Added one owner-authorized primary Planner board per personal project, immutable scene revisions,
  stable node identities, migration backfill, scene/history APIs, ETags, and UTF-8 payload limits.
- Replaced browser authority with debounced cloud autosave while retaining ordered IndexedDB recovery;
  offline, failed, unavailable-recovery, embedded-file, and revision-conflict states are explicit.
- Hardened concurrent saves at both the aggregate and PostgreSQL unique-constraint boundary so two
  simultaneous stale writes produce one success and one 409 instead of a silent overwrite or 500.
- Verified migration/backfill and real HTTP ownership, restore, stale-tab, and concurrent-write paths
  against a disposable PostgreSQL database. Backend tests are 12/12; frontend tests are 230/230;
  build, lint/design lint, Storybook build, and EF model-drift checks pass.

## 2026-08-28 (Planner Phase 17 — immersive shell and project context)

- Moved `/member/planner` to its own authenticated full-viewport route so Excalidraw owns the complete
  `100dvw × 100dvh` browser surface instead of inheriting the member shell's width, padding, and scroll.
- Added compact project/progress/save/tool overlays, creator-owned project selection, remembered last
  project, loading/error/empty states, and an inline personal-project creation drawer.
- Kept scenes isolated by user and project in temporary browser storage; Phase 18 must replace this
  with server-authorized boards, revisions, recovery cache, and concurrency conflict UX.
- Desktop (1280×720) and mobile (390×844) component previews pass; frontend build, lint/design lint,
  and Storybook build pass. Five focused specs compile, but ChromeHeadless crashes in the host GPU
  sandbox before Jasmine executes.

## 2026-08-27 (Planner requirements and end-to-end roadmap committed)

- Added `docs/PLANNER.md` as the canonical context for every future Planner discussion.
- Defined the full-viewport project-scoped workspace, Excalidraw/TaskFlow source-of-truth boundary,
  cloud persistence and concurrency, canonical work-item links, admin-versioned templates, secure
  resources, immutable primary requirement baselines, and New/Changed/Removed comparison history.
- Scheduled implementation as `docs/PHASES.md` Phases 17–23. **Next implementation phase is Phase 17;
  no Planner code or schema change was made in this documentation session.**
- Updated OVERVIEW and ProjectCompletion so a new chat following normal documentation entry points
  discovers the committed Planner roadmap immediately.

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
