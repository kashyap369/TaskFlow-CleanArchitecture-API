# TaskFlow — Project Overview

## What It Is
TaskFlow is a Task Management System API (ASP.NET Core + PostgreSQL, Clean Architecture/DDD/CQRS) serving **two kinds of accounts**:

1. **Individual (normal user)** — a person managing their own tasks.
2. **Organization (company)** — a company workspace with an owner, custom roles, teams, members, projects, and detailed reporting.

Intended client: an Angular frontend (CORS already configured for http://localhost:4200). The `AccountType` enum (Individual / Organization) already exists in the Domain and drives this split.

## Planner — committed roadmap

Planner is the next end-to-end product capability: a project-scoped, full-viewport Excalidraw
workspace for defining projects, arranging tasks/subtasks, attaching supporting resources, tracking
progress, and preserving a primary requirement baseline plus every later New/Changed/Removed delta.
Excalidraw owns canvas interaction and layout; TaskFlow remains authoritative for business data,
ownership, files, progress, and history.

The complete durable requirements, boundaries, domain model, persistence rules, acceptance criteria,
and Phases 17–23 are in **[PLANNER.md](PLANNER.md)**. Read it before any Planner discussion or work.
Phases 17–18 are complete: the immersive project workspace now saves authorized, revisioned scenes to
the API, retains IndexedDB only for recovery, and surfaces offline and optimistic-concurrency conflicts.
Canonical linked work objects and the remaining resource/history capabilities begin in Phase 19.

## Individual Users
> ✅ **Complete end to end (Phase 9, 2026-07-26)** — API *and* Angular client, verified live:
> register → **verify email** → sign in → create personal tasks with subtasks → start / complete /
> **reopen** → track time → `GET /api/report/me?from&to`. A personal task is visible **only to its
> creator** (any other user gets 403 on every read and write).

- Register and manage a personal workspace.
- Create tasks with subtasks; track lifecycle (Todo → InProgress → Completed, reopen).
- Personal tracking & reports: how many tasks created/completed and when — weekly/monthly/yearly views.

## Organizations
- **Owner (boss)** registers the organization and controls it.
- **Custom roles** — the owner defines roles that fit their company (e.g. Manager, Developer, Designer, HR); roles carry **permissions** (e.g. "can create projects", "can assign tasks").
- **Invitations** — owner (or permitted roles) invites members by email; invitee accepts/rejects; owner can cancel. Members can be activated/deactivated, removed, or moved to another role.
- **Teams** — group members into teams like "Developer Team", "Designer Team". Tasks and reports can be viewed per team.
- **Tasks & assignment** — org tasks (standalone or under a project) get assigned to members, optionally filtered role-wise (e.g. a Manager assigns a design task to someone in the Designer role/team).
- **Projects** — permission-designated members create projects containing tasks/subtasks, then assign which members work on which task. Project views show which task belongs to which project and who it's assigned to.

## Reporting & Dashboard (a headline feature)
A strong dashboard backed by the Dapper read side, focused on:
- Which team performed which tasks, and in what duration
- Weekly / monthly / yearly detail reports for a single member and for a team
- Task reports (created vs completed, overdue, by status/priority)
- Project reports (progress, workload distribution, timeline)
- Time & tracking — durations from start → completion per task/member/team

## Candidate Additional Features (backlog ideas, not committed)
- Due dates with reminders + in-app/email notifications
- Task comments and file attachments
- Activity feed / audit trail per project and organization
- Tags/labels, search and filtering, kanban-style board endpoints
- Report export (CSV/PDF)
- Recurring tasks (for individuals especially)

## Where the Code Stands vs This Vision

> Audited line-by-line against this document on 2026-07-26. Coverage: **Organization ~85% · Reporting
> ~75% · Individual 100%.** The side-by-side view (API vs the Angular client, per feature) lives in
> [ProjectCompletion.md](ProjectCompletion.md) §3.0 — **both portals are now built and consuming the API.**

**The Individual account is implemented and verified end to end (Phase 9):**
- **Sign-up → email verification → sign-in** (`POST /auth/verify-email`, `/auth/resend-verification`).
- Personal tasks (no organization) with subtasks, start/complete/**reopen**/update/delete, time tracking.
- Per-user isolation enforced on reads **and** writes.
- `GET /report/me?from&to` for weekly / monthly / yearly personal tracking.

**The Organization half is implemented and verified end-to-end:**
- **Organizations**: orgs, custom roles with a **permission** catalog + grant/revoke, members, **email invitations**, and **teams**.
- **Work management**: projects, tasks (with **assignment** to members), subtasks, task lifecycle, and **work logs** (time tracking — live timer + manual).
- **Reporting**: Dapper-powered dashboard summary + member / team / project reports.
- **Security**: JWT + refresh-token rotation, `[Authorize]` on all controllers, org-permission checks on writes, and read-side org scoping (IDOR guard).

**Not yet implemented:**
- 🟡 **Teams group people, not work.** `Task` has no `TeamId` and there is no team-task query, so
  "tasks … viewed per team" above is only true of *reports*.
- 🟡 **Assignment is not role-filterable.** `AssignTaskCommandHandler` checks only that the assignee is
  an active member; the Designer-role example above isn't expressible.
- 🟡 **Report cuts missing:** no priority breakdown, no project timeline, and team reports give counts
  rather than *which* tasks.

**Known defects** (details in [ProjectCompletion.md](ProjectCompletion.md) §4): organization
update/delete have **no authorization** (§4.3); `GET /user/{id}` has no Admin bypass (§4.2);
`RemoveMember` and `DeactivateMember` are the same operation (§4.4).
*Fixed in Phase 9:* task/subtask/work-log write authorization (§4.1b) and domain-exception → 400
mapping (§4.5).

**Remaining polish:** pagination/filtering on lists, `ApiResponse<T>` envelope consistency,
forgot-password endpoint, and automated tests. See [PHASES.md](PHASES.md).
