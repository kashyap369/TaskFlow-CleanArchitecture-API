# TaskFlow — Project Overview

## What It Is
TaskFlow is a Task Management System API (ASP.NET Core + PostgreSQL, Clean Architecture/DDD/CQRS) serving **two kinds of accounts**:

1. **Individual (normal user)** — a person managing their own tasks.
2. **Organization (company)** — a company workspace with an owner, custom roles, teams, members, projects, and detailed reporting.

Intended client: an Angular frontend (CORS already configured for http://localhost:4200). The `AccountType` enum (Individual / Organization) already exists in the Domain and drives this split.

## Individual Users
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
Already built (write side): users + JWT/refresh auth, organizations, org roles, members, invitations, projects, tasks, subtasks, task lifecycle.
Not built yet: `AccountType` on User/registration, teams, task assignment, real permissions (`OrganizationPermission` is an empty stub), time tracking, the entire read/reporting side (Dapper stub), invitation emails, dashboards. See [PHASES.md](PHASES.md) for the plan.
