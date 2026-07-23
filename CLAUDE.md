# TaskFlow — Claude Code Guide

## Summary
TaskFlow is a Task Management System REST API (ASP.NET Core, PostgreSQL) with two account types: **Individual** users (personal tasks/subtasks + tracking reports) and **Organizations** (owner, custom roles with permissions, email invitations, teams, task assignment, projects, and a reporting dashboard — team/member/project reports, time tracking). Architecture: Clean Architecture, DDD, CQRS (MediatR), Domain Events, Repository Pattern (write side), Dapper (read side — implemented via `ISqlConnectionFactory`), FluentValidation, JWT auth with refresh-token rotation. An Angular frontend (localhost:4200) is the intended client. Product vision: docs/OVERVIEW.md; roadmap: docs/PHASES.md.

## Solution Layout
- `TaskFlow.Api` — controllers (thin, MediatR only), middlewares (exception handling, request logging), auth policies, `CurrentUserService`, response envelopes
- `TaskFlow.Application` — Features/{Module}/{Entity}/Commands/{FeatureName}/ (command + handler + validator), pipeline behaviors, contracts (security, email), domain event handlers, exceptions
- `TaskFlow.Domain` — entities (rich, behavior-first), value objects, domain events, enums, repository interfaces, constants. Depends on nothing.
- `TaskFlow.Infra` — EF Core (Npgsql) persistence, repositories, UnitOfWork, domain event dispatcher, JWT/password security, SMTP email, seeders, migrations

Modules: **Identity** (users, roles, auth), **Organizations** (orgs, members, org roles, invitations), **WorkManagement** (projects, tasks, subtasks).

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

## Docs
- [docs/OVERVIEW.md](docs/OVERVIEW.md) — what the API is, who it's for, domain concepts
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — layers, request flow, auth, domain events, persistence
- [docs/CONVENTIONS.md](docs/CONVENTIONS.md) — naming patterns + new-endpoint checklist
- [docs/PHASES.md](docs/PHASES.md) — roadmap and current status (check this first each session)
- [docs/SESSIONS.md](docs/SESSIONS.md) — session log: gotchas, dead ends, decisions

## Session Habit
At the end of each working session: update PHASES.md status and append a short entry to SESSIONS.md.
