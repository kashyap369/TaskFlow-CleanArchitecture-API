# TaskFlow — Phases & Status

> Keep the Current Status section up to date at the end of every session.

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

## Phase 2 — Security ✅ (one leftover)
JWT bearer auth, refresh-token rotation with reuse detection, logout, role policies (Admin/Manager/User), seeders, current user from token.
Leftover: apply `[Authorize]` to Organization/WorkManagement controllers (deliberately open during dev).

## Phase 3 — Account Types & Individual Experience
- Add `AccountType` (enum exists) to `User` + registration flow: Individual vs Organization signup.
- Individual workspace rules: personal tasks/subtasks not tied to an organization (Task.OrganizationId currently required — needs to become optional or use a personal-workspace concept).
- Email verification endpoint flow (entity method exists, no endpoint/token yet).
- Invitation emails actually sent on InviteUser (email infra exists, not wired to invitations).

## Phase 4 — Read Side Foundation (Dapper)
- Implement `DapperContext` (currently an empty stub) + query conventions (`Features/{Module}/{Entity}/Queries/{Name}/`).
- Basic queries: get-by-id and lists for users, organizations, members, roles, invitations, projects, tasks, subtasks (org-scoped, paginated, filtered).
- Adopt `ApiResponse<T>` envelope consistently across all controllers.
- Turn on `[Authorize]` policies everywhere (closes Phase 2 leftover).

## Phase 5 — Teams & Task Assignment
- `Team` entity (per organization) + member↔team membership; e.g. Developer Team, Designer Team.
- Task assignment: assign org tasks/project tasks to members (role-wise/team-wise filtering when assigning).
- Assignment lifecycle events (assigned/reassigned/unassigned) for later reporting.

## Phase 6 — Permissions
- Implement `OrganizationPermission` (currently an empty stub): permission catalog (create-project, assign-task, invite-member, manage-roles, ...), attached to org roles.
- Permission checks in handlers (e.g. only permitted roles create projects) — org-level authorization distinct from system-role policies.

## Phase 7 — Time & Tracking
- Capture durations: task started→completed timestamps already exist (StartDate/ActualCompletionDate); add explicit time-entry or work-session tracking per member per task.
- Foundation data for duration-based reports.

## Phase 8 — Reporting & Dashboard (headline feature)
Dapper-powered aggregate queries:
- Team performance: which team did which tasks, in what duration
- Member & team detail reports: weekly / monthly / yearly
- Task reports: created vs completed, overdue, by status/priority
- Project reports: progress, member workload, timeline
- Dashboard summary endpoints for the Angular frontend charts

## Backlog (unscheduled)
- Due-date reminders + notifications (in-app/email)
- Task comments & attachments
- Activity feed / audit trail
- Tags/labels, search, kanban board endpoints
- Report export (CSV/PDF), recurring tasks
- Redis caching, Hangfire background jobs, event bus
- Unit + integration tests, Docker, CI/CD

<!-- Phase ordering rationale: account types before read side because individual/org scoping
     changes query shapes; teams+assignment before permissions because permissions gate
     assignment actions; time tracking before reporting because reports consume its data.
     Owner may reorder. -->
