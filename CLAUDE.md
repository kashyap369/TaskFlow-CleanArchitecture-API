# TaskFlow — Claude Code Guide

## Repository map (BOTH sides of this product)

TaskFlow is **two repositories on one machine**. Most tasks touch one; many need to be checked against
the other. Always confirm which side owns the work before writing code.

| | Backend (this repo) | Frontend |
|---|---|---|
| **Path** | `D:\Projects\TMS\TaskFlow` | `D:\Projects\TMS\TaskFlowUI\TaskFlowApp` |
| **Stack** | ASP.NET Core 8 + PostgreSQL, Clean Architecture / DDD / CQRS | Angular 20, standalone + signals, Atomic Design, multi-portal |
| **Git remote** | `https://github.com/kashyap369/TaskFlow-CleanArchitecture-API.git` | `https://github.com/kashyap369/taskflow-ui.git` |
| **Dev URL** | `https://localhost:7086/api` (also `http://localhost:5138`) | `http://localhost:4200` (the only origin CORS allows) |
| **Guide** | `D:\Projects\TMS\TaskFlow\CLAUDE.md` | `D:\Projects\TMS\TaskFlowUI\TaskFlowApp\CLAUDE.md` |
| **Docs** | `D:\Projects\TMS\TaskFlow\docs\` | `D:\Projects\TMS\TaskFlowUI\TaskFlowApp\docs\` |

Each repository has its own remote and its own commit history — **commit them separately**; there is no
monorepo and no shared branch.

**Cross-repo rules**
- [docs/ProjectCompletion.md](docs/ProjectCompletion.md) (backend) is the **single parity ledger for both
  repositories**. Read it first to decide which side a task belongs to; update it in the same session as
  any work that changes the API surface or what the UI consumes.
- [docs/MEETINGS.md](docs/MEETINGS.md) (backend) is the **canonical cross-repository Meetings roadmap**.
  The frontend's `docs/MEETINGS.md` is only a pointer — never duplicate the roadmap there.
- Everything else is per-repo: read the local `docs/` set for the side you are editing.
- When a change spans both sides, update **both** doc sets (PHASES + SESSIONS on each) plus the ledger.

## Summary
TaskFlow is a Task Management System REST API (ASP.NET Core, PostgreSQL) with two account types: **Individual** users (personal tasks/subtasks + tracking reports) and **Organizations** (owner, custom roles with permissions, email invitations, teams, task assignment, projects, and a reporting dashboard — team/member/project reports, time tracking). Architecture: Clean Architecture, DDD, CQRS (MediatR), Domain Events, Repository Pattern (write side), Dapper (read side — implemented via `ISqlConnectionFactory`), FluentValidation, JWT auth with refresh-token rotation. An Angular frontend (localhost:4200) is the intended client. Product vision: docs/OVERVIEW.md; roadmap: docs/PHASES.md.

## Solution Layout
- `TaskFlow.Api` — controllers (thin, MediatR only), middlewares (exception handling, request logging), auth policies, `CurrentUserService`, response envelopes
- `TaskFlow.Application` — Features/{Module}/{Entity}/Commands/{FeatureName}/ (command + handler + validator), pipeline behaviors, contracts (security, email), domain event handlers, exceptions
- `TaskFlow.Domain` — entities (rich, behavior-first), value objects, domain events, enums, repository interfaces, constants. Depends on nothing.
- `TaskFlow.Infra` — EF Core (Npgsql) persistence, repositories, UnitOfWork, domain event dispatcher, JWT/password security, SMTP email, seeders, migrations

Modules: **Identity** (users, roles, auth), **Organizations** (orgs, members, org roles + permissions, invitations, teams), **WorkManagement** (projects, tasks, subtasks, work logs / time tracking), **Reporting** (dashboard + member/team/project reports).

## Commands
```
dotnet build
dotnet run --project TaskFlow.Api        # Swagger at /swagger in Development
dotnet ef migrations add <Name> --project TaskFlow.Infra --startup-project TaskFlow.Api
```
DB: PostgreSQL, connection string `DefaultConnection`. Seeds on startup: system roles (Admin/Manager/User) + admin user `admin@taskflow.com` / `Admin@123`.

## Hard Rules
- Controllers stay thin: no business logic, only `_mediator.Send(...)`. Business rules live in Domain entities; orchestration in handlers.
- Never trust IDs from request bodies for identity/ownership — derive the current user from the JWT via `ICurrentUserService`.
- All writes go through repositories + `IUnitOfWork.SaveChangesAsync()`. Handlers never touch EF Core directly.
- All reads (queries) use Dapper via `ISqlConnectionFactory`, not EF Core. Query record + handler live in one file under `Queries/{Name}/`; see docs/CONVENTIONS.md for the raw-SQL rules (quote identifiers, filter `IsDeleted`, UTC dates).
- Entity state changes only via entity methods (e.g. `user.Suspend()`), never property setters. Raise domain events inside entity methods for significant business events.
- Errors: throw the typed exceptions from `TaskFlow.Application/Exceptions` (NotFound/Conflict/Unauthorized/Forbidden/Business) with a SCREAMING_SNAKE code — the exception middleware maps them to HTTP responses.
- Input validation: FluentValidation validator per command, wired automatically via `ValidationBehavior`.
- Deletes are soft deletes (`AuditableEntity.SoftDelete()`); a global query filter hides deleted rows.
- **Authorization has three layers** (see docs/ARCHITECTURE.md): (1) every controller has `[Authorize]` — new controllers must too; auth endpoints stay anonymous. (2) Write commands that touch org-permission-sensitive actions call `IOrganizationPermissionChecker` (owner bypasses; else role must hold the permission from `OrganizationPermissionNames`). (3) Read queries returning org-scoped data implement an access-scope marker interface (`Application/Common/Authorization`) so `AccessGuardBehavior` enforces owner/member access — do this for every new org-scoped query.

## Docs
- [docs/ProjectCompletion.md](docs/ProjectCompletion.md) — **API ⇄ UI parity ledger: the one doc covering
  *both* projects.** Which features exist on each side, which endpoints the frontend consumes, what's
  blocking whom. **Read it first** to see whether a task belongs to this repo or the frontend.
- [docs/OVERVIEW.md](docs/OVERVIEW.md) — what the API is, who it's for, domain concepts
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — layers, request flow, auth, domain events, persistence
- [docs/CONVENTIONS.md](docs/CONVENTIONS.md) — naming patterns + new-endpoint checklist
- [docs/PHASES.md](docs/PHASES.md) — roadmap and current status (check this first each session)
- [docs/MEETINGS.md](docs/MEETINGS.md) — canonical cross-repository Meetings roadmap and session
  handoff contract. Read it completely for any meeting, LiveKit, guest-link, badge, collaboration or
  recording task; Phase 0 is complete and Phase 1 is READY.
- [docs/SESSIONS.md](docs/SESSIONS.md) — session log: gotchas, dead ends, decisions
- [infra/meetings/RUNBOOK.md](infra/meetings/RUNBOOK.md) — **production meetings triage.** What broke
  on 2026-09-04, how each fault was proved, how to read LiveKit logs, and how configuration survives
  a redeploy. Read this before diagnosing a meeting failure — three of the four faults looked like
  networking and none were.

## Session Habit
At the end of each working session: update PHASES.md status, append a short entry to SESSIONS.md, **and
update [docs/ProjectCompletion.md](docs/ProjectCompletion.md)** if the work changed the API surface or
closed something the frontend was blocked on (add a Changelog row + fix the affected parity rows).
Nothing fails when that ledger drifts — it only stays true if it's updated deliberately.
