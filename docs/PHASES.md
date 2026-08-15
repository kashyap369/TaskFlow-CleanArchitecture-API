# TaskFlow — Phases & Status

> Keep the Current Status section up to date at the end of every session.

## ✅ Phase 16 — Creator-only personal projects (2026-08-15)

- Personal projects have `OrganizationId = null` and take `CreatedByUserId` exclusively from the JWT.
  Joining an organization never changes their scope or makes them visible to its members.
- Added `POST /project/personal` and `GET /project/mine/personal`; existing project get/update/delete
  routes now authorize organization projects through membership/permissions and personal projects
  through exact creator ownership.
- Personal tasks may reference only a private project owned by that same creator. Existing task,
  subtask, and work-log guards continue the creator-only boundary downstream.
- Migration `AddPrivatePersonalProjects` applied to the configured development database. Live tests
  proved bidirectional cross-user project reads return 403, including an admin reading another user's
  private project.
- Development object storage can now use the local filesystem, so the API runs without Docker; the S3
  provider remains the production default.

## ✅ Phase 15 — Project authorization and joined-workspace access (2026-08-15)

- Secured project creation with `CreateProject` and project update/delete with `ManageProjects`.
  Organization owners retain the existing permission-checker bypass; other callers must be active
  members whose organization role carries the required permission.
- Closed the direct-API path that allowed any authenticated system user to create, edit, or delete
  projects by supplying a known organization/project ID.
- The Angular client now lets Individual accounts enter organizations they have joined while keeping
  the personal portal as their default workspace. No endpoint or database migration was required.
- `dotnet build` succeeds; the solution's pre-existing nullability warnings remain.

## ✅ Phase 14 — Account recovery & passwordless sign-in (2026-08-14)

- Added four public auth endpoints: `POST /auth/password/forgot`, `POST /auth/password/reset`,
  `POST /auth/login-code/request`, and `POST /auth/login-code/verify`.
- One shared `OneTimeCode` lifecycle supports reset and login purposes with HMAC-SHA256 hashes,
  cryptographically generated six-digit codes, 10-minute expiry, single use, five-attempt lockout,
  60-second resend cooldown, generic request responses, and per-IP API rate limiting.
- Password reset revokes all active refresh tokens. Passwordless sign-in uses the same JWT, role,
  account-status checks, and portal-routing response as password sign-in.
- Migration `AddOneTimeCodes` generated and applied locally. API endpoint count **82 → 86**.

## ▶ NEXT SESSION — START HERE (2026-07-26)

**Phases 10–13 are DONE — the backend is feature-complete.** Organization, Reporting and Admin are
now at 100%: every ⬜/🟡/⚠️ row in `docs/ProjectCompletion.md` §3.0 and §4 is closed. Endpoints
**76 → 82**. One migration (`AddTaskTeamAndPlatformSettings`), applied and verified live.

**Three live security holes were closed** (org update/delete had *no* authorization; all four
organization-member commands had *none either* — found during this work and not in any backlog).

**⏭️ The remaining work is entirely on the frontend**, and none of it is blocked any more. See the
frontend's `docs/PHASES.md` Phases 24–29:
- **24–25** never needed the backend (CSV/PDF export, trend charts, calendar page).
- **26** is now pure verification — the admin user-detail drawer works with **zero frontend change**.
- **27, 28, 29** are unblocked by Phases 13, 11 and 12 respectively; the endpoints are live and tested.

Backend backlog that remains is all non-blocking: automated tests (still none), pagination on list
endpoints, `ApiResponse<T>` envelope consistency, Docker/CI.

## Current Status (2026-07-23, IDOR fix)
- ✅ **Read-side org scoping (IDOR) resolved.** New `IOrganizationAccessGuard` (Infra, EF) + `AccessGuardBehavior` (MediatR pipeline). Read queries implement a marker interface (`IOrganizationScopedRequest`/`IProjectScopedRequest`/`ITaskScopedRequest`/`ITeamScopedRequest`/`IRoleScopedRequest`/`IUserScopedRequest`/`IMemberReportScopedRequest`); the behavior resolves the id to an organization and verifies the current user is the owner or an active member before the handler runs. Personal tasks → creator only; user profile → self or shared org; member report → self or owner of a shared org.
- Marked on all 19 org/project/task/team/role/user/report read queries; the "my …" queries and the permission catalog stay unmarked (already self-scoped or public). Commands are NOT marked — they keep their own `IOrganizationPermissionChecker` checks.
- Verified live: owner (admin) → 200 on org 1; non-member (jane) → 403 on org 1 org/dashboard/tasks/team/role; jane sees her own profile (200) but not admin's (403); jane's own "my" queries → 200.

## Current Status (2026-07-23, security pass)
- ✅ **`[Authorize]` applied to every controller** (closes the Phase 2 leftover). All controllers require `AuthorizationPolicies.AllRoles` (any authenticated user) — fine-grained org authorization already runs in the handlers via `IOrganizationPermissionChecker`. `UserController.GetAll` (list all users) is `AdminOnly`. AuthController (register/login/refresh/logout) stays anonymous.
  - Verified: unauthenticated requests → 401 across query/command/report endpoints; login stays open; admin token reaches `/user/me` and the AdminOnly user list.
- ⚠️ **Known gap → do next: read-side org scoping (IDOR).** Read queries that take an `organizationId`/`projectId`/`userId` (e.g. `GET /report/dashboard/{orgId}`, `GET /organization/{id}`, `GET /task/organization/{orgId}`) do NOT yet verify the caller belongs to that org. Any authenticated user can read another org's data by guessing IDs. Fix: check membership/ownership in the read handlers (reuse `IOrganizationPermissionChecker` or add a membership check). This is the top remaining security item.
- ⏭️ Also open: pagination/filtering on list queries; `ApiResponse<T>` envelope consistency; email verification endpoint; tests.

## Current Status (2026-07-23, later)
- ✅ **Application layer complete — write side + read side (Dapper).**
  - Write: registration takes `AccountType`; teams (create/update/delete/add-member/remove-member); task assign/unassign; role grant/revoke permission; work logs (start/stop/manual/delete); invitation email handler. Org-permission enforcement via new `IOrganizationPermissionChecker` (owner bypasses; else role must have the permission).
  - Read: Dapper foundation (`ISqlConnectionFactory` + `SqlConnectionFactory`, Dapper added to Application). ~25 queries across all modules + 4 reports (dashboard summary, member task report, team performance, project report) under `Features/Reporting/`.
  - Controllers: new Team/WorkLog/Report/User controllers; query GETs + new command endpoints added to existing controllers.
  - Fixed a pre-existing defect: `RefreshToken.RevokedByIp`/`ReplacedByToken` were NOT NULL but only set on revoke → **login threw a 500 on every attempt**. Made them nullable (entity + config), migration `MakeRefreshTokenRevocationNullable` applied.
  - Verified end-to-end on localhost:5138: login→JWT, `/user/me`, register with account type, create org, `/organization/mine`, create team, grant permission + role detail, create task, dashboard aggregate — all correct.
- ⏭️ Next: apply `[Authorize]` policies to controllers (dev-stage open); pagination/filtering on list queries; more report endpoints if needed.

## Current Status (2026-07-23)
- ✅ Phases 1–2 done (write side + auth/security)
- 📌 Product vision defined (two account types, teams, permissions, assignment, reporting dashboard) — see [OVERVIEW.md](OVERVIEW.md)
- ✅ **Domain layer for the vision is built** (migration `DomainVisionFoundation` generated, NOT yet applied to the DB):
  - `User.AccountType` (Individual default / Organization)
  - `Task.OrganizationId` now nullable → personal tasks; `Task.AssignedToUserId` + `Assign`/`Unassign` with domain events; `TaskCompletedEvent`
  - `Team` + `TeamMember` entities
  - `OrganizationPermission` catalog + `OrganizationRolePermission`; `OrganizationRole.Grant/Revoke/HasPermission`; permission names in `Domain/Constants/OrganizationPermissionNames`
  - `TaskWorkLog` (live timer + manual entry) for time tracking
  - New repo interfaces: `ITeamRepository`, `IOrganizationPermissionRepository`, `ITaskWorkLogRepository`
  - `OrganizationMemberInvitedEvent` raised on invitation creation (no email handler yet)
- ✅ **Infra layer complete for the vision:**
  - `TeamRepository`, `OrganizationPermissionRepository`, `TaskWorkLogRepository` implemented + registered in `Infra/DependencyInjection/DependencyRegistration.cs`
  - `OrganizationPermissionSeeder` populates the catalog from `OrganizationPermissionNames`; wired into `Program.cs` after role/user seeders
  - Migration applied; app boots clean and seeds all 9 permissions (verified end-to-end on localhost:5138)
- ⏭️ Next: Application layer — commands/handlers for teams, task assignment, role permissions, work logs; registration with account type; `OrganizationMemberInvitedEvent` email handler (Application, register in Infra DI)

## Phase 1 — Core Write Side ✅
Domain model (DDD entities, value objects, domain events), commands/handlers/validators for Identity, Organizations, WorkManagement. EF Core persistence, soft deletes, exception middleware, request logging, welcome email on registration.

## Phase 2 — Security ✅
JWT bearer auth, refresh-token rotation with reuse detection, logout, role policies (Admin/Manager/User), seeders, current user from token. `[Authorize]` now applied to every controller (AllRoles; AdminOnly on the user list; auth endpoints anonymous).
Remaining security hardening tracked at top: read-side org scoping (IDOR).

## Phase 3 — Account Types & Individual Experience ✅ (one leftover)
- ✅ `AccountType` on `User` + registration flow (Individual vs Organization).
- ✅ Personal tasks: `Task.OrganizationId` now nullable; `GetMyPersonalTasks` query.
- ✅ Invitation emails wired (`OrganizationMemberInvitedEvent` → email handler).
- ⏭️ Leftover: email verification endpoint flow (entity method exists, no endpoint/token yet).

## Phase 4 — Read Side Foundation (Dapper) ✅ (leftovers)
- ✅ `ISqlConnectionFactory` + `SqlConnectionFactory` (replaced the empty `DapperContext` stub); ~25 queries across all modules under `Features/{Module}/{Entity}/Queries/{Name}/`.
- ⏭️ Leftover: pagination/filtering on list queries; adopt `ApiResponse<T>` envelope consistently across all controllers.
- ⏭️ Leftover: turn on `[Authorize]` policies everywhere (also closes Phase 2 leftover).

## Phase 5 — Teams & Task Assignment ✅
- ✅ `Team` + `TeamMember`; create/update/delete/add-member/remove-member.
- ✅ Task assign/unassign with domain events (assigned/unassigned/completed); assignee must be an active org member.

## Phase 6 — Permissions ✅
- ✅ `OrganizationPermission` catalog + `OrganizationRolePermission`; grant/revoke on roles; seeded catalog.
- ✅ `IOrganizationPermissionChecker` enforced in handlers (owner bypasses; else role must hold the permission).

## Phase 7 — Time & Tracking ✅
- ✅ `TaskWorkLog` (live timer via start/stop + manual entry); per-user/per-task queries with computed durations.

## Phase 8 — Reporting & Dashboard (headline feature) ✅
- ✅ Dapper aggregate queries: dashboard summary, member task report, team performance, project report (progress + per-member workload). Weekly/monthly/yearly via From/To window.
- ⏭️ Possible extensions: more report cuts (by status/priority, timelines), export.

## Phase 9 — Individual Account: the personal workspace ✅ DONE (2026-07-26)

> **Shipped and verified end-to-end against the running API.** An Individual account now has a real
> workspace: create personal tasks, subtasks with auto-complete, full lifecycle, time tracking, and a
> personal report. **Endpoint count 71 → 73** (`POST /task/personal`, `GET /report/me`). Every
> organization flow was regression-tested and is unchanged.
>
> ### What was actually changed
> | File(s) | Change |
> |---|---|
> | `CreateTaskCommand` | `int OrganizationId` → **`int? OrganizationId`** |
> | `CreateTaskCommandValidator` | org id validated only `.When(HasValue)`; new rule: `ProjectId` must be null without an org |
> | `CreateTaskCommandHandler` | org lookup is now conditional; **added `EnsureOrganizationAsync`** (it previously let *any* user create a task in *any* org); typed `PROJECT_REQUIRES_ORGANIZATION` conflict |
> | `TaskController` | new **`POST /task/personal`**; `POST /task` now 400s on a missing org id instead of silently creating a personal task |
> | `Models/Requests/CreatePersonalTaskRequest.cs` | new — no OrganizationId, no ProjectId by construction |
> | **11 command handlers** (task ×4, subtask ×5, worklog ×2) | **added `EnsureTaskAsync`** — see 9.0 |
> | `ExceptionHandlingMiddleware` | `ArgumentException` / `InvalidOperationException` → **400**, not 500 |
> | `GetMyPersonalTaskReportQuery` + `PersonalTaskReportDto` | new — personal report over a From/To window |
> | `ReportController` | new **`GET /report/me?from&to`** |
> | `IOrganizationAccessGuard` | doc updated — it now guards writes too |
>
> **No migration. No domain change. No schema change.** `Task.OrganizationId` was already `int?` and the
> column already nullable.
>
> ### Verified live (results)
> - `POST /task/personal` → task 5 with `organizationId: null`; appears in `GET /task/mine/personal`.
> - Subtask added → completed → **parent auto-completed (status 3, 1/1)** → reopened → deleted.
> - `PUT /task/5/start` 204 · `PUT /task` (update) 204 · manual work log 204 →
>   `GET /worklog/mine?from&to` returns it.
> - `GET /report/me?from&to` → `tasksCreated 1, tasksInProgress 1, trackedHours 2.5`.
> - `PUT /task/5/assign/2` → **400 `TASK_NOT_ASSIGNABLE`** ("Personal tasks cannot be assigned."), not 500.
> - `POST /task` with no org id → **400 `ORGANIZATION_ID_REQUIRED`**, never a silent personal task.
> - **Cross-tenant writes (admin, who is in no org, against org 2's task 3): start / complete / delete /
>   subtask / worklog / update → all 403.** Every one of those succeeded before this phase.
> - **Personal isolation (a second real user vs the admin's personal task): read / start / complete /
>   delete / subtask / worklog → all 403**, and it does not appear in her own personal list.
> - **Org regression as the org owner:** list org tasks 200, read task 200, create 200, start 204,
>   assign 204, dashboard 200 — unchanged. All test data removed; seed state restored.
>
> ### Phase 9 continued — closing the last two Individual gaps (same day)
> An audit against `OVERVIEW.md` found the workspace worked but the **account lifecycle** didn't:
> - **§4.6 Task reopen** — `SubTask` had `Reopen()`, `Task` did not, so a completed task was one-way.
>   Added `Task.Reopen()` (no-op unless Completed; clears `ActualCompletionDate`; defers to
>   `RecalculateStatus()` when subtasks exist) + `ReopenTaskCommand` + **`PUT /task/{id}/reopen`**.
> - **§4.7 Email verification** — a new account was PendingVerification with **no way to verify**, so
>   nobody could ever sign up. Added **`POST /auth/verify-email`** + **`POST /auth/resend-verification`**
>   with a **stateless HMAC token** (`userId.expiry.signature`, JWT secret, 48h) — **no schema change**.
>   The welcome email carries the link. Resend always 200s so it can't enumerate accounts.
> - **Bug found by the new UI: `TaskWorkLogs.Notes` was NOT NULL** while the domain allowed null, so
>   starting a timer without a note **500'd**. `Notes` is now `string?` + `IsRequired(false)`; migration
>   `MakeWorkLogNotesNullable`. (Same class as the earlier `RefreshToken` nullability bug — a
>   non-nullable CLR string silently becomes a NOT NULL column.)
>
> **Endpoints 73 → 76.** Verified live: register → 401 `EMAIL_NOT_VERIFIED` → verify → **sign in works**;
> complete → reopen → start again; timer with no notes now 200s.
>
> ### Follow-up noted (not a defect)
> `GET /worklog/mine` and `GET /report/me` **require** `?from&to`; omitting them silently returns empty
> (the model binder defaults to `0001-01-01`). Clients must always send a window.

<details>
<summary>Original plan (kept for context)</summary>

> **Goal:** make every promise `OVERVIEW.md` makes about the **Individual** account true. Today an
> Individual can register, sign in, and accept an invitation into someone *else's* organization —
> nothing more. This phase gives them a workspace of their own.
>
> **Hard constraint: do not disturb the Organization feature set.** Every step below is additive or a
> nullability widening. Where a shared code path changes, the organization branch keeps its exact
> current behaviour and is covered by an acceptance test.

### 9.A Survey — why this is much smaller than it looks

Traced through the whole stack before planning. **Four of the five layers already model personal tasks:**

| Layer | State | Evidence |
|---|:--:|---|
| **Domain** | ✅ ready | `Task.OrganizationId` is `int?`; the constructor takes `int? organizationId` and validates it; `IsPersonal => !OrganizationId.HasValue`; `Assign()` throws for a personal task; a personal task may not have a `ProjectId` |
| **Database** | ✅ ready | The model snapshot has `b.Property<int?>("OrganizationId")` with **no `.IsRequired()`** — the column is already nullable. **No migration is needed.** |
| **Infra / repository** | ✅ ready | `TaskRepository.GetByTitleAsync(int? organizationId, …)` already branches: org tasks clash org-wide, personal tasks clash only within the same user's workspace |
| **Read authorization** | ✅ ready | `OrganizationAccessGuard.EnsureTaskAsync` already handles it: org task → owner/member check; personal task → **creator only** |
| **Read queries** | ✅ ready | `GetMyTasks`, `GetMyPersonalTasks` exist and are exposed as `GET /task/mine`, `/task/mine/personal`; `GET /worklog/mine` too |
| **Application write layer** | ⛔ **the only blocker** | `CreateTaskCommand` takes a non-nullable `int OrganizationId`, and its handler unconditionally loads the organization and 404s |

**So the feature is one nullable parameter away — plus the security work in 9.0, which is not optional.**

### 9.0 ⚠️ PREREQUISITE — write-side authorization on tasks and subtasks

**Found while surveying: four command handlers enforce nothing at all.**

| Handler | Authorization present |
|---|---|
| `DeleteTaskCommandHandler` | **none** — loads by id, removes, saves |
| `StartTaskCommandHandler` | **none** |
| `CompleteTaskCommandHandler` | **none** |
| `CreateSubTaskCommandHandler` | **none** |

Any authenticated user can start, complete or delete **any task by id**, and attach a subtask to it.
`AccessGuardBehavior` doesn't cover this — by design it only inspects *read* requests
(`AccessScopedRequests.cs`: *"Commands are NOT marked — their handlers enforce permissions directly"*),
and these handlers don't.

Today the blast radius is limited to guessing ids inside an organization. **The moment personal tasks
exist, this becomes private personal data that anyone can mutate.** Fix it first.

**Approach (smallest correct change):** reuse the guard that already encodes the right rule.
`EnsureTaskAsync` resolves a task to *"owner/active member for an org task, creator for a personal
task"* — exactly the write rule too. Either:
- **(a)** mark these commands with `ITaskScopedRequest` so `AccessGuardBehavior` runs for them, or
- **(b)** inject `IOrganizationAccessGuard` and call `EnsureTaskAsync` explicitly at the top of each handler.

**Recommend (b)** — it keeps the "commands enforce their own permissions" convention the codebase
already documents, and keeps the behavior read-only, so no existing query changes meaning. Revisit
whether org-task *writes* should additionally consult `IOrganizationPermissionChecker` (deferred:
today's behaviour is unchanged either way, so this phase must not silently tighten org rules).

### 9.1 Make `OrganizationId` optional on task creation

- `CreateTaskCommand`: `int OrganizationId` → `int? OrganizationId`.
- `CreateTaskCommandValidator`: `RuleFor(x => x.OrganizationId).GreaterThan(0).When(x => x.OrganizationId.HasValue);`
- `CreateTaskCommandHandler`:
  - Only resolve/verify the organization **when `OrganizationId.HasValue`** — the existing
    `ORGANIZATION_NOT_FOUND` path is untouched for org tasks.
  - Reject `ProjectId` without an organization with a typed `ConflictException`
    (`PROJECT_REQUIRES_ORGANIZATION`) **before** constructing the entity — the domain already throws
    `ArgumentException` for this, which today surfaces as a 500 (see 9.B).
  - `GetByTitleAsync` needs no change; pass the nullable through.

### 9.2 Expose it — two thin routes, one command

Add `POST /task/personal` alongside `POST /task`, both delegating to the same command:

| Route | Behaviour |
|---|---|
| `POST /task` | `OrganizationId` **required** — 400 if absent. Unchanged for existing clients. |
| `POST /task/personal` | `OrganizationId` and `ProjectId` **must be absent** — 400 if present. Creator comes from the JWT. |

**Why two routes rather than one nullable field:** with a single endpoint, a client bug that drops
`organizationId` would silently create a *personal* task instead of failing — a data-integrity trap
that is invisible until someone notices missing org tasks. Two routes make intent explicit and cost
two thin controller actions, which matches the "controllers stay thin" rule.

### 9.3 Verify the rest of the lifecycle works unchanged on a personal task

No code expected here — confirm and add tests:
- **Update / Start / Complete / Delete** — these handlers have no org logic at all, so they already work
  once 9.0 adds the ownership check.
- **Subtasks** — `SubTask` hangs off `TaskId` with no org coupling; parent auto-complete already works.
- **Work logs** — `StartWorkLogCommandHandler` checks only `ExistsAsync(taskId)` + the one-running-timer
  rule, and takes the user from the JWT. Should work as-is. ⚠️ It also has **no ownership check** —
  fold it into 9.0.
- **Assignment must stay refused** — `Task.Assign()` throws for a personal task. Confirm the API surfaces
  a clean 4xx, not a 500 (see 9.B).

### 9.4 Personal reports — "weekly / monthly / yearly views"

`OVERVIEW.md` promises personal tracking reports. `GET /report/member/{userId}` is marked
`IMemberReportScopedRequest` (self, or an owner of a shared org), so an Individual *can* call it for
themselves — but the query is written around organization membership.

**Add `GET /report/me?from&to`** → a `PersonalTaskReportDto`: tasks created / completed / in progress /
overdue + tracked hours, over the window, across the caller's **personal** tasks. Same Dapper shape as
`MemberTaskReportDto`, filtered by `"OrganizationId" IS NULL AND "CreatedByUserId" = @UserId`. Scoped
to the caller from the JWT, so it needs no marker interface.

### 9.B Related defects to fix in the same pass (they surface immediately here)

- **Domain `ArgumentException` → 500.** The API middleware doesn't map it to 400. Personal tasks make
  this reachable in three new ways ("a personal task cannot belong to a project", "personal tasks cannot
  be assigned", and the existing worklog future-time case). Map `ArgumentException` → **400** in the
  exception middleware, or convert those domain guards to typed exceptions.
- Frontend already guards the worklog case client-side; the mapping should still exist.

### 9.C Explicitly NOT in this phase

Organization behaviour must come out byte-identical. Not touched: org task creation semantics, projects,
teams, roles/permissions, invitations, members, assignment rules, all org reports, and every existing
route's contract. Also deferred (they are §3.0 vision gaps, not Individual-account work): task↔team
link, role-filtered assignment, report priority breakdown, project timeline.

### 9.D Acceptance criteria

1. `POST /task/personal` creates a task with `OrganizationId = null`; `GET /task/mine/personal` returns it.
2. Subtasks, start/complete, update and delete all work on that task.
3. Start/stop/manual work logs attach to it; `GET /worklog/mine` returns them.
4. `GET /report/me?from&to` reports it.
5. **Assigning it returns 4xx, not 500.**
6. **A second user cannot read, start, complete, delete, subtask or log time against it** (this is 9.0).
7. `POST /task` **without** `organizationId` returns **400** — never a silent personal task.
8. **Every existing organization flow behaves exactly as before** — org task create/update/assign/
   lifecycle, projects, reports. This is the regression bar for the phase.

### 9.E Order of work

`9.0 (security)` → `9.1` → `9.2` → `9.B` → `9.3` (verify) → `9.4` → docs.
9.0 first because it is a live defect *and* the thing that makes personal data safe to introduce.

**Unblocks on the frontend:** roadmap Phase 5 (the whole Member portal) plus the 3 previously
unconsumable endpoints. See `ProjectCompletion.md` §4.1.

</details>

---

## ✅ DONE — Path to 100%: Organization, Reporting, Admin (Phases 10–13, 2026-07-26)

> **All four phases shipped and verified live against the running API.** Individual was done in
> Phase 9; these closed every remaining ⬜/🟡/⚠️ row in `ProjectCompletion.md` §3.0 and §4 for
> Organization, Reporting and Admin. **Endpoints 76 → 82.**
>
> ### What shipped
>
> | Phase | Result |
> |---|---|
> | **10** | §4.3, §4.2, §4.4 closed — **plus a fourth hole found mid-work** (see below) |
> | **11** | `Task.TeamId` + `GET /team/{id}/tasks` + role-filtered member queries |
> | **12** | Priority breakdown, project timeline, per-team task lists |
> | **13** | `GET /admin/organizations`, `GET`/`PUT /admin/settings` + a real settings entity |
>
> ### 🚨 Found while implementing Phase 10 — in no backlog before now
> **All four `OrganizationMember` command handlers enforced nothing at all.** `RemoveMember`,
> `DeactivateMember`, `ActivateMember` and `ChangeMemberRole` loaded by id and mutated — no ownership
> check, no permission check. **Any authenticated user could deactivate, remove or re-role any member
> of any organization by guessing ids.** Same family as §4.1b (Phase 9.0) and §4.3, and it was
> genuinely live: verified before the fix that the seeded admin — who belongs to no organization —
> could act on org 2's members. All four now call
> `IOrganizationPermissionChecker.EnsurePermissionAsync(ManageMembers)`; verified **403** afterwards.
>
> `ChangeMemberRole` had a second bug: it validated that the role *exists* but not that it belongs to
> the **same organization**, so a role id from another org was accepted, silently importing that org's
> permission set. Now 409 `ROLE_ORGANIZATION_MISMATCH`.
>
> ### Migration
> **`AddTaskTeamAndPlatformSettings`** — purely additive: nullable `Tasks.TeamId`, its index, and the
> new `PlatformSettings` table. No column drops, no alters, no NOT NULL added to existing rows.
> Applied and verified.
>
> ### Design decisions worth keeping
> - **Team assignment is its own route** (`PUT /task/{id}/team/{teamId}`, `DELETE /task/{id}/team`),
>   *not* a field on `UpdateTaskCommand`. A form-filled update whose DTO lacked the field would have
>   silently cleared the team on every save — the exact trap that already bit task *description* and
>   organization *description* in this project. A dedicated command cannot be invoked by accident.
> - **Org update/delete are owner-only**, not "owner or member". `EnsureOrganizationAsync` permits
>   active members, which is far too weak for renaming or deleting a whole workspace, so Phase 10 added
>   `EnsureOrganizationOwnerAsync`.
> - **The admin bypass is only on `EnsureUserAsync`**, deliberately not on organization data. A
>   platform admin can open any *user profile* (which is what `GET /user` already implied); it does not
>   become a skeleton key for every organization's tasks and reports.
> - **Maintenance mode has two escape hatches** so enabling it can never lock an admin out:
>   `/api/auth/*` is always open, and authenticated admins pass through everything. Both verified live.
> - **Settings are enforced, not decorative.** `RegistrationOpen` is checked in
>   `RegisterUserCommandHandler` (403 `REGISTRATION_CLOSED`); `MaintenanceMode` by
>   `MaintenanceModeMiddleware` (503). A setting nothing reads is worse than no setting, because the
>   UI implies it works.
> - Both settings **fail open** when the settings row is missing, matching behaviour from before they
>   existed.
>
> ### Verified live (results)
> - **§4.3:** admin (owner of org 1, not org 2) → `PUT`/`DELETE` on org 2 = **403
>   `NOT_ORGANIZATION_OWNER`**; "Northwind Labs" unchanged. Owner rename still **204** (renamed and
>   restored).
> - **§4.2:** admin → `GET /user/2` = **200** with full detail. Previously 403. **Unblocks the
>   frontend drawer with no frontend change.**
> - **§4.4:** owner removes a member → member **vanishes** from the list (2 → 1). Deactivate → member
>   **stays listed** as Inactive (still 2). The two commands are finally different. Seed row restored
>   via SQL.
> - **New hole:** all four member commands → **403 `PERMISSION_DENIED`** for a non-member.
> - **Phase 11:** task 3 → team 2 → appears in `GET /team/2/tasks` with `teamName "Engineering"`.
>   Cross-org team → **409 `TEAM_ORGANIZATION_MISMATCH`**; missing team → **404**; personal task →
>   **409 `TEAM_REQUIRES_ORGANIZATION`**. Create-with-`teamId` works; `DELETE .../team` clears it.
>   Member filters: `?activeOnly=true` 2→1, `?organizationRoleId=2` → 2, `roleId=99` → 0.
> - **Phase 12:** dashboard returns `highPriorityTasks: 2`; project report returns all four timeline
>   dates; team report now names *"Build the responsive nav bar"* with its 2.5 tracked hours.
> - **Phase 13:** admin lists both orgs with owner/member/project/task counts; settings read + write;
>   non-admin → **403** on `/admin/*`. Registration closed → **403 `REGISTRATION_CLOSED`**.
>   Maintenance on → normal user **503** with the admin's message, admin **200**, login **200**.
> - **Regression:** 19/19 org reads 200. Full org task lifecycle with the **old request shape**
>   (create → start → assign → update → complete → reopen → unassign → delete) all pass. Personal-task
>   lifecycle + `/report/me` unchanged. `dotnet build` **0 warnings, 0 errors**.
> - **All test data removed; seed state restored.** One deliberate exception: **task 3 is left on team
>   2 (Engineering)** as sample data for the frontend's Phase 28. Clear it with
>   `DELETE /api/task/3/team` if you want a pristine seed.

### Phase 10 — Security fixes & admin unblock (small, no schema changes)
Closes §4.2, §4.3, §4.4.

| # | Item | Change |
|---|---|---|
| 10.1 | **§4.3 Org update/delete authorization** (live security hole — any authenticated user can rename/delete any org by id) | Add an ownership check to `UpdateOrganizationCommandHandler` / `DeleteOrganizationCommandHandler` (only the owner may act), or mark both with a new `IOrganizationScopedRequest`-style write-side check consistent with `IOrganizationAccessGuard`. Same pattern as the 9.0 task-guard fix. |
| 10.2 | **§4.2 Admin bypass on `GET /user/{id}`** | `EnsureUserAsync` (in `AccessGuardBehavior`) currently permits only "self or shares an org with the caller." Add a short-circuit: if the caller holds the `Admin` role, allow. Unblocks the frontend's admin user-detail drawer platform-wide with **zero frontend change** — it's already built and already renders a denied state today. |
| 10.3 | **§4.4 `RemoveMember` vs `DeactivateMember`** | Decision needed, then implement: recommend keeping both endpoints but making `RemoveMember` an actual removal (soft-delete the `OrganizationMember` row) so a removed member stops appearing in the members list entirely, while `DeactivateMember` keeps today's reversible-toggle behavior. Update the handler; no route change. |

**Acceptance:** non-owner `PUT`/`DELETE /organization` → 403. Admin token → `GET /user/{id}` → 200 for any user. Removed member no longer listed; deactivated member still listed as Inactive.

### Phase 11 — Task↔Team link & role-filtered assignment (medium, schema change)
Closes the Organization §3.0 rows: "tasks viewed per team" and "assignment filtered role-wise."

| # | Item | Change |
|---|---|---|
| 11.1 | `Task.TeamId` (nullable, org tasks only) | Domain: add nullable `TeamId` + a `Task.AssignToTeam`/constructor param; EF migration `AddTaskTeamId`. Personal tasks must stay `TeamId == null` (mirror the `OrganizationId` nullability rule from Phase 9). |
| 11.2 | `GetTeamTasks` query + `GET /team/{teamId}/tasks` | New Dapper query + route, marked `ITeamScopedRequest` (or reuse the existing team-scope guard) so it goes through `AccessGuardBehavior` like every other team read. |
| 11.3 | `CreateTaskCommand` / `UpdateTaskCommand` gain optional `TeamId` | Validate the team belongs to the same organization as the task; no team ⇒ unchanged behavior (fully backward compatible). |
| 11.4 | Role-filtered assignment | `AssignTaskCommandHandler` currently checks only `IsActiveMemberAsync`. Add an optional `roleId` filter to the "assignable members" read query (whatever currently backs the assignee dropdown) so the UI can filter candidates by role before assigning — the OVERVIEW.md example is "a Manager assigning a design task to someone in the Designer role." |

**Acceptance:** a task created with a `teamId` appears in `GET /team/{teamId}/tasks`; a task with no team is unaffected; the assignable-members query accepts `?roleId=` and narrows correctly; personal tasks reject a `teamId` with 400.

### Phase 12 — Reporting depth (small–medium, additive to existing Dapper queries)
Closes the Reporting §3.0 rows: priority breakdown, project timeline, "which tasks" per team.

| # | Item | Change |
|---|---|---|
| 12.1 | Priority breakdown | Add a `TasksByPriority` (or per-priority counts) column set to the dashboard-summary and/or member-report Dapper queries — additive, no new endpoint. |
| 12.2 | Project timeline | Add `StartDate` / `ExpectedCompletionDate` / `ActualCompletionDate` (already on `Project`/`Task` entities — no schema change) to `ProjectReportDto`. |
| 12.3 | "Which tasks" per team | Once **Phase 11** ships `Task.TeamId`, extend `TeamPerformanceReportDto` (or add a nested list) with the actual task titles/ids per team, not just counts + `AvgCompletionDays`. **Depends on 11.1.** |

**Acceptance:** dashboard/member report DTOs carry a priority breakdown; `ProjectReportDto` carries the three dates; team performance report lists the tasks it's summarizing.

### Phase 13 — Admin platform features (medium, new surface area)
Closes the two Admin rows with **no endpoint at all** — the biggest remaining gap in the whole ledger.

| # | Item | Change |
|---|---|---|
| 13.1 | `GET /admin/organizations` (AdminOnly) | New Dapper query returning every organization (id, name, owner, active-member count, status, created date) — today only `GET /organization/mine` exists (caller's own). New controller action or extend `OrganizationController`, gated `AdminOnly`. |
| 13.2 | Platform settings | Decide scope first (recommend a minimal single-row settings table: app name, whether self-registration is open, maintenance-mode flag). New entity + migration + `GET`/`PUT /admin/settings`, both `AdminOnly`. |

**Acceptance:** an admin token lists every org platform-wide (not just its own); settings are readable and writable by Admin only, 403 for everyone else.

### Suggested backend order
`10` (security + unblock, ~1 session) → `11` and `13` can run in parallel (independent) → `12` last (12.3 needs 11 done first; 12.1/12.2 could move earlier if convenient — they have no dependency).

---

## Backlog (unscheduled)
- Due-date reminders + notifications (in-app/email)
- Task comments & attachments
- Activity feed / audit trail
- Tags/labels, search, kanban board endpoints
- Recurring tasks
- Redis caching, Hangfire background jobs, event bus
- Unit + integration tests, Docker, CI/CD

<!-- Phase ordering rationale: account types before read side because individual/org scoping
     changes query shapes; teams+assignment before permissions because permissions gate
     assignment actions; time tracking before reporting because reports consume its data.
     Owner may reorder. -->
