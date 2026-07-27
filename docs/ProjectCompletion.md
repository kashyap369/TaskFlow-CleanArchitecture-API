# TaskFlow — Project Completion Ledger (API ⇄ UI)

> **The one place that talks about *both* projects.** Every other doc describes one side; this file is
> the side-by-side parity view: what the API exposes, what the UI consumes, what's bound, what's live,
> and who is blocking whom.
>
> | | |
> |---|---|
> | **Backend** | `D:\Projects\TMS\TaskFlow` — ASP.NET Core 8 + PostgreSQL, Clean Architecture / DDD / CQRS (EF write side, Dapper read side) |
> | **Frontend** | `D:\Projects\TMS\TaskFlowUI\TaskFlowApp` — Angular 20, standalone + signals, Atomic Design, multi-portal |
> | **Dev URLs** | API `https://localhost:7086/api` · UI `http://localhost:4200` (CORS allows only this origin) |
>
> **Last verified:** 2026-07-26, after frontend Phases 26–29 (endpoint counts re-derived from the
> controllers and the frontend `API` map: **82 exposed, 80 consumed**).

---

## ▶ UPDATE PROTOCOL — read this before editing

**Whenever work lands on either side, update this file in the same session.** It is the only doc that
goes stale silently, because nothing in either build fails when it drifts.

1. Update the affected row(s) in [§3 Feature parity](#3-feature-parity-matrix).
2. If an endpoint was added or consumed, update the counts in [§2](#2-at-a-glance) **and** re-derive
   them rather than incrementing by hand:
   - **API total:** count `[HttpGet|HttpPost|HttpPut|HttpDelete|HttpPatch]` across
     `TaskFlow.Api/Controllers/**` (exclude `WeatherForecastController`).
   - **UI bound:** list the distinct `API.*` constants referenced in
     `src/app/features/*/*.repository.ts` **plus** `core/interceptors/refresh.interceptor.ts`
     (`/auth/refresh` is called there, not from a repository — it is easy to miss).
3. Add a line to [§7 Changelog](#7-changelog).
4. Keep the two per-project doc sets as the detail; **don't duplicate them here.** This file answers
   *"are the two sides in sync?"* — not *"how does the org portal work?"*

**Status keys:** ✅ done · 🟡 partial · ⬜ not started · ⛔ blocked (someone else must move first)

---

## 1. The goal

One product, two account types, two codebases:

- **Individual** — a person managing their own tasks, subtasks, lifecycle and personal reports.
- **Organization** — a company workspace: an owner, custom roles with a permission catalog,
  invitations, members, teams, projects, tasks with assignment, time tracking, and a reporting
  dashboard as the headline feature.

**"Done" means:** the API exposes the endpoint, the UI consumes it through a real screen, and the pair
has been exercised against a running API — not that the code compiles.

---

## 2. At a glance

| | Backend | Frontend |
|---|---|---|
| **Phases complete** | **13 of 13** (+ a security pass and an IDOR fix) | **27 session phases**; roadmap Phases 0–8 ✅ (Phase 6 admin extras closed by session Phase 27) |
| **Version status** | **Feature-complete.** Both account types + Organization/Reporting/Admin at 100% | **All three portals complete** (session Phases 26–29, 2026-07-26) |
| **Endpoints** | **82** exposed (13 controllers; excludes the Angular-template `WeatherForecastController`) | **80 consumed.** The only 2 left are the deliberate skips below — **nothing is unconsumed for want of a screen** |
| **Quality gates** | `dotnet build` **0 warnings / 0 errors** · **no automated tests** (backlog) | `ng lint` **0 errors** · `ng build` ✅ · **204/204** unit tests · WCAG AA on all 42 token pairings · **no E2E** |
| **Biggest gap** | **None blocking.** Automated tests, pagination, `ApiResponse<T>` consistency, forgot-password | Calendar page, CSV/PDF export, per-member trend charts, E2E tests (V1-GAPS §3) |
| **Vision coverage** (§3.0) | **Organization 100% · Reporting 100% · Individual 100%** | **All three portals complete** — Organization, Individual and Admin |

> **2026-07-26 — the two sides are now in sync.** Backend Phases 10–13 closed every ⛔/⚠️/⬜ row, and
> frontend Phases 26–29 consumed the six endpoints they added. **What remains on either side is
> optional polish, not parity:** forgot-password on the API, V1-GAPS §3 on the UI.

### The 2 unconsumed endpoints — deliberate, not a gap

| Endpoint | Why unconsumed |
|---|---|
| `GET /task/mine` | Tasks *assigned* to the caller across organizations. The org portal already lists org tasks and the member portal lists personal ones; a third combined view isn't in the product description. |
| `GET /worklog/mine` | All the caller's work logs. The time drawer shows per-task logs and `/report/me` gives the totals, so this would be a third rendering of the same data. **Note: it requires `?from&to`** — omitting them binds `0001-01-01` and returns `[]`. |

Both are live and correct; nothing is blocked on them.

---

## 3. Feature parity matrix

### 3.0 Vision coverage — `OVERVIEW.md` read as a spec

The matrices below track *endpoints*. This table tracks the **product promises in `docs/OVERVIEW.md`**,
which is a different question — an endpoint can exist while the feature it was meant to deliver doesn't.
Audited line by line on 2026-07-26; the ⬜ rows appear in **no other backlog**.

**Individual account — 3 of 3 promises met, on both sides. ✅ COMPLETE**

| OVERVIEW promise | API | UI | Reality |
|---|:--:|:--:|---|
| "Register and manage a personal workspace" | ✅ | ✅ | Register → verify email → sign in → `/member/my-tasks`. Verified live end to end. |
| "Create tasks with subtasks; track lifecycle (Todo → InProgress → Completed, **reopen**)" | ✅ | ✅ | Start / Done / **Reopen** on the row; subtask drawer with parent auto-complete. |
| "Personal tracking & reports — weekly/monthly/yearly" | ✅ | ✅ | Member dashboard: live task counts + `GET /report/me` with week/month/year presets. |

> **Isolation is enforced and proven**: a second real user gets **403** on every read *and* write against
> another user's personal task, and it never appears in their list.

**Organization account — 6 of 6 promises met on the API. ✅ COMPLETE (2026-07-26, Phase 11)**

| OVERVIEW promise | API | UI | Reality |
|---|:--:|:--:|---|
| Owner registers + controls the organization | ✅ | ✅ | Full CRUD both sides; **§4.3 authorization fixed in Phase 10** |
| Custom roles carrying permissions | ✅ | ✅ | Catalog + grant/revoke + editor UI |
| Invitations and member lifecycle | ✅ | ✅ | Both directions; **§4.4 fixed in Phase 10** |
| Projects containing tasks/subtasks, members assigned per task | ✅ | ✅ | |
| **Teams — "tasks and reports can be viewed per team"** | ✅ | ✅ | **API Phase 11 / UI Phase 28**: `Task.TeamId` + `GET /team/{id}/tasks`. Team-tasks panel on `team-detail-page`; a team `<select>` on every task row (rendered from `teamName`, written through the dedicated routes) |
| **Assignment "optionally filtered role-wise"** | ✅ | ✅ | **API Phase 11 / UI Phase 28**: `?organizationRoleId=&activeOnly=` narrows the candidate list. The UI keeps a separate `assignableMembers` signal (always `activeOnly=true`) plus a role select. Still a *filter*, not a restriction |

**Reporting (headline feature) — 5 of 5 promises met on the API. ✅ COMPLETE (2026-07-26, Phase 12)**

| OVERVIEW promise | API | UI | Reality |
|---|:--:|:--:|---|
| Weekly / monthly / yearly for a member and a team | ✅ | ✅ | `?from&to` windows |
| Time & tracking durations per task/member/team | ✅ | ✅ | |
| "Which team performed **which tasks**, and in what duration" | ✅ | ✅ | **API Phase 12 / UI Phase 29**: `TeamPerformanceReportDto.Tasks` rendered as an expandable row on the team-performance table, labelled to distinguish *owned* tasks from the member-based aggregate counts |
| "Task reports … by status/**priority**" | ✅ | ✅ | **API Phase 12 / UI Phase 29**: `DashboardSummaryDto`'s priority counts render as a second donut beside the status one on the org dashboard |
| "Project reports (progress, workload, **timeline**)" | ✅ | ✅ | **API Phase 12 / UI Phase 29**: the five dates render as a planned-vs-actual two-bar timeline on the project report |

**Admin — 2 of 2 promises met on the API. ✅ COMPLETE (2026-07-26, Phase 13)**

| Promise | API | UI | Reality |
|---|:--:|:--:|---|
| **List every organization** | ✅ | ✅ | **API Phase 13 / UI Phase 27.1**: `/admin/organizations` page — stat tiles, search across name + owner + owner email, status filter, paging, read-only detail drawer (no second request; the list DTO carries everything) |
| **Platform settings** | ✅ | ✅ | **API Phase 13 / UI Phase 27.2**: `/admin/settings` form through the validation engine, sending all five fields. The UI states that both flags are enforced, and a 503 `MAINTENANCE_MODE` raises a sticky banner at the app root rather than one toast per failed request |

**Coverage: Organization 100% · Reporting 100% · Individual 100% · Admin 100% — on *both* sides.**

Every ⬜ above is now **frontend-only**: the endpoint exists, is tested and is live. Nothing on either
side is waiting on the other.


Legend: **API** = endpoint(s) exist · **UI** = a real screen consumes it · **Live** = exercised against
a running API.

### 3.1 Identity & access

| Feature | Endpoints | API | UI | Live | Notes |
|---|---|:--:|:--:|:--:|---|
| Register (Individual / Organization) | `POST /auth/register` | ✅ | ✅ | ✅ | Account type drives the post-login portal |
| Login + role-based redirect | `POST /auth/login` | ✅ | ✅ | ✅ | 3 entry screens (solo / org / admin), one component |
| Refresh-token rotation | `POST /auth/refresh` | ✅ | ✅ | ✅ | UI: `core/interceptors/refresh.interceptor.ts`, single-flight lock |
| Logout | `POST /auth/logout` | ✅ | ✅ | ✅ | Wired from all three portal top bars |
| Current user profile | `GET /user/me` | ✅ | ✅ | ✅ | |
| List all users (AdminOnly) | `GET /user` | ✅ | ✅ | ✅ | Admin Users page: stat tiles, search, filters, pager |
| Open a user | `GET /user/{id}` | ✅ | ✅ | ✅ | **§4.2 fixed (Phase 10)** — admins can now open any profile; the drawer needed no change |
| Email verification | `POST /auth/verify-email`, `/auth/resend-verification` | ✅ | ✅ | ✅ | **Phase 9.** Welcome email carries the link; `/auth/verify-email` page handles it, with resend |
| Forgot / reset password | — | ⬜ | ⛔ | — | No endpoint. The "Forgot?" link is inert (`href="#"`) |

### 3.2 Organization

| Feature | Endpoints | API | UI | Live | Notes |
|---|---|:--:|:--:|:--:|---|
| Create organization | `POST /organization` | ✅ | ✅ | ✅ | Onboarding card on the org dashboard |
| Rename / update | `PUT /organization` | ✅ | ✅ | ✅ | **§4.3 fixed (Phase 10)** — owner-only, 403 otherwise |
| Delete | `DELETE /organization/{id}` | ✅ | ✅ | ✅ | Type-the-name confirmation in the UI; **owner-only in the API** |
| My organizations | `GET /organization/mine` | ✅ | ✅ | ✅ | Feeds the sidebar org switcher |
| Organization detail | `GET /organization/{id}` | ✅ | ✅ | ✅ | Settings page loads this — the list DTO has no `description` |
| **All organizations (admin)** | `GET /admin/organizations` | ✅ | ✅ | ✅ | **API Phase 13 / UI Phase 27.1.** `/admin/organizations`, read-only (no admin write route exists, and org write is owner-only) |
| **Platform settings** | `GET`/`PUT /admin/settings` | ✅ | ✅ | ✅ | **API Phase 13 / UI Phase 27.2.** Verified live: `PUT 204`, `updatedAt` moved, maintenance really 503'd a non-admin and was restored |

### 3.3 Roles, permissions, members, invitations

| Feature | Endpoints | API | UI | Live | Notes |
|---|---|:--:|:--:|:--:|---|
| Roles CRUD | `POST` `PUT` `DELETE /organizationrole`, `GET /organization/{id}`, `GET /{roleId}` | ✅ | ✅ | ✅ | 5 endpoints |
| Permission catalog + grant/revoke | `GET /permissions`, `POST /grant-permission`, `POST /revoke-permission` | ✅ | ✅ | ✅ | Toggle-switch editor drawer; grants by permission **name** |
| Members list / activate / deactivate / change role / remove | 5 endpoints on `/organizationmember` | ✅ | ✅ | ✅ | **§4.4 + §4.3b fixed (Phase 10)** — remove really removes; all four commands now require `ManageMembers`. List accepts `?organizationRoleId=&activeOnly=` |
| Invitations — send / cancel / list | `POST /invite`, `POST /cancel`, `GET /organization/{id}` | ✅ | ✅ | ✅ | Requires a role to exist first (creating an org seeds none) |
| Invitations — accept / reject / mine | `POST /accept`, `POST /reject`, `GET /mine` | ✅ | ✅ | ✅ | Page mounted in **both** portals — invitations are user-scoped |

**Roles 8/8 · Members 5/5 · Invitations 6/6 bound.**

### 3.4 Work management

| Feature | Endpoints | API | UI | Live | Notes |
|---|---|:--:|:--:|:--:|---|
| Projects CRUD + detail | 5 on `/project` | ✅ | ✅ | ✅ | List + detail page; create/edit share one drawer |
| Tasks CRUD + detail | `POST` `PUT` `DELETE` `GET /{id}` | ✅ | ✅ | ✅ | `TaskListItem` has no `description` — edit fetches the detail first |
| Task lists (org / project) | `GET /task/organization/{id}`, `/project/{id}` | ✅ | ✅ | ✅ | |
| Task lifecycle | `PUT /{id}/start`, `/complete`, `/reopen` | ✅ | ✅ | ✅ | **Reopen added in Phase 9** — completes the documented Todo→InProgress→Completed→reopen cycle |
| Task assignment | `PUT /{id}/assign/{userId}`, `/unassign` | ✅ | ✅ | ✅ | Inline assignee `<select>` on every row |
| **Personal tasks** | `POST /task/personal`, `GET /task/mine/personal` | ✅ | ✅ | ✅ | Member portal **My tasks** page: filters, paging, create/edit drawer, lifecycle |
| Subtasks (add / rename / complete / reopen / delete / list) | 6 on `/subtask` | ✅ | ✅ | ✅ | Parent task auto-completes when all subtasks complete |
| Teams CRUD + detail + members | 7 on `/team` | ✅ | ✅ | ✅ | Team detail page with add/remove member |
| **Team-scoped tasks** | `GET /team/{id}/tasks`, `PUT /task/{id}/team/{teamId}`, `DELETE /task/{id}/team` | ✅ | ✅ | ✅ | **API Phase 11 / UI Phase 28.** Team assignment is its own route, never a field on `PUT /task`; the UI's edit drawer honours that and calls it **only when the team changed**. All three verified live |
| Work log — timer, manual, delete, per task | 5 on `/worklog` | ✅ | ✅ | ✅ | One running timer per user (409 otherwise) |
| **Personal time tracking** | `/worklog/*` on a personal task | ✅ | ✅ | ✅ | Timer + manual entry in the My-tasks time drawer |

### 3.5 Reporting & dashboard (the headline feature)

| Feature | Endpoints | API | UI | Live | Notes |
|---|---|:--:|:--:|:--:|---|
| Dashboard summary | `GET /report/dashboard/{orgId}` | ✅ | 🟡 | ✅ | Stat tiles + task-status donut. **Phase 12 added a priority breakdown the UI doesn't render yet** (frontend 29.1) |
| Member report | `GET /report/member/{userId}?from&to` | ✅ | ✅ | ✅ | Six stat tiles per member |
| Team performance | `GET /report/team/{orgId}?from&to` | ✅ | 🟡 | ✅ | echarts bar chart + table. **Phase 12 added the per-team task list** (frontend 29.3) |
| Project report | `GET /report/project/{projectId}` | ✅ | 🟡 | ✅ | Completion ring + workload. **Phase 12 added 5 timeline dates** (frontend 29.2) |
| Date windows (week/month/year/all) | query params | ✅ | ✅ | ✅ | |
| **Personal report (Individual)** | `GET /report/me?from&to` | ✅ | ✅ | ✅ | Member dashboard, week/month/year presets |
| **Export (CSV / PDF)** | n/a | ⬜ | ⬜ | — | **Frontend-doable today** — the page already holds every figure (§5) |
| **Per-member trend charts** | reuse `?from&to` | ✅ | ⬜ | — | **Frontend-doable today** — loop the existing endpoint (§5) |

### 3.6 Cross-cutting

| Concern | Backend | Frontend |
|---|---|---|
| AuthZ | ✅ `[Authorize]` everywhere, org-permission checks on writes, read-side IDOR guard, **owner-only org update/delete and `ManageMembers` on every member command (Phase 10)** | Route guards per portal; owner gates are **usability only**, never a security boundary |
| Response shape | ⚠️ `/auth/*` returns `ApiResponse<T>`; **everything else returns raw DTOs** | Repositories typed per endpoint to match — a known trap when adding one |
| Pagination | ⬜ No endpoint accepts page/size | ✅ Client-side over the full list (`createPagination` + `Pagination` molecule) — swap the source when the API pages |
| Error mapping | ✅ Domain exceptions → 400 (§4.5, Phase 9.B) | Central `errorInterceptor`; the client-side worklog guard can now be relaxed |
| Design system / theming / a11y | n/a | ✅ Atomic Design + Storybook, light/dark tokens, WCAG AA enforced by `npm run a11y:contrast` |
| Automated tests | ⬜ **None** (backlog) | ✅ 164 unit specs · ⬜ no E2E |
| Docker / CI-CD | ⬜ Backlog | ⬜ |

---

## 4. Open issues — the blocking list

### 4.1 ✅ RESOLVED (Phase 9, 2026-07-26) — personal-task write path
`CreateTaskCommand` takes a **non-nullable `int OrganizationId`** and its handler loads the organization,
so a task can never be created without one. The domain permits `Task.OrganizationId == null` and
`GET /task/mine/personal` exists to read such rows — but nothing can produce them.

**Effect:** the **Individual account type has no workspace**. That is half the stated product vision.
It also strands 3 endpoints and blocks frontend roadmap Phase 5.
**Fixed:** `CreateTaskCommand.OrganizationId` is now `int?`, exposed as **`POST /task/personal`**
(with `POST /task` 400-ing on a missing org id rather than silently creating a personal task). No
migration and no domain change were needed — the survey found Domain, DB column, repository, read guard
and read queries already supported it. Verified end-to-end; see Phase 9 in [PHASES.md](PHASES.md).
> ⚠️ `docs/OVERVIEW.md` currently lists *"personal tasks (nullable org)"* under what's implemented, and
> says the vision is done end-to-end. Correct that when this is fixed — or before.

### 4.1b ✅ RESOLVED (Phase 9.0, 2026-07-26) — task/subtask/work-log write authorization
Found while planning Phase 9. `DeleteTaskCommandHandler`, `StartTaskCommandHandler`,
`CompleteTaskCommandHandler` and `CreateSubTaskCommandHandler` load by id and mutate — **no ownership
check, no org check, no permission check.** `AccessGuardBehavior` does not cover them: by design it
inspects only *read* requests (`AccessScopedRequests.cs` — *"Commands are NOT marked"*).

**Any authenticated user can start, complete or delete any task by id, and attach a subtask to it.**
`StartWorkLogCommandHandler` has the same hole.

**Fixed in Phase 9.0, ahead of the feature**, because personal tasks would have inherited the hole.
**11 command handlers** (task ×4, subtask ×5, work-log ×2) now call
`IOrganizationAccessGuard.EnsureTaskAsync`, which already encoded the right rule (org task →
owner/active member; personal task → creator only). `CreateTask` also gained `EnsureOrganizationAsync`
— it previously let any authenticated user plant a task in any organization.

**Verified:** as a user belonging to no organization, `start` / `complete` / `delete` / `subtask` /
`worklog` / `update` against another org's task all return **403** — every one of them succeeded
before. A second real user gets **403** on all six against another user's personal task.
`StopWorkLog` / `DeleteWorkLog` already checked work-log ownership and were left alone.

### 4.6 ✅ RESOLVED (Phase 9, 2026-07-26) — a completed task can now be reopened
`SubTask` has `Reopen()`; **`Task` does not**, and there is no `PUT /task/{id}/reopen` route.
`Task.Start()` throws *"Completed task cannot be started."*, so once a task is Completed it is
**one-way** — unless it happens to have subtasks, where reopening a subtask flips the parent back via
`RecalculateStatus()`.

`OVERVIEW.md` promises the lifecycle *"Todo → InProgress → Completed, **reopen**"* for both account
types, so this is a gap for organizations too — it just matters more for personal tasks, which are
usually checklist-shaped and have no subtasks. **In no other backlog before 2026-07-26.**
**Fixed:** `Task.Reopen()` (no-op unless Completed; clears `ActualCompletionDate`; defers to
`RecalculateStatus()` when the task has subtasks so they keep owning the parent's state) +
`ReopenTaskCommand` + **`PUT /task/{taskId}/reopen`**. Both portals show a **Reopen** action on a
completed row. Verified live in the API and in the browser.

### 4.7 ✅ RESOLVED (Phase 9, 2026-07-26) — a new account can now sign in
Verified live: `POST /auth/register` succeeds, then `POST /auth/login` returns **401
`EMAIL_NOT_VERIFIED`** — and **no verification endpoint exists**. Every usable account today was either
seeded or flipped manually in the database.

This makes the Individual account **unreachable for a real new user**: the workspace behind the door
works, but the door doesn't open. It blocks the frontend's forgot-password / verify-email screens too.
**Fixed:** **`POST /auth/verify-email`** + **`POST /auth/resend-verification`**, backed by a
**stateless HMAC token** (`userId.expiry.signature`, signed with the JWT secret, 48-hour life) — so no
schema change was needed. The existing welcome email now carries the link; `UserRegisteredEventHandler`
resolves the new user by email (the event is raised before the row exists, so it can't carry the id).
Resend always returns 200 so it can't be used to enumerate accounts. The frontend has an
`/auth/verify-email` page, and **register now redirects there instead of to a login form that cannot
work yet**. Verified live: register → 401 EMAIL_NOT_VERIFIED → verify → **sign in succeeds**.

### 4.2 ✅ RESOLVED (Phase 10, 2026-07-26) — `GET /user/{id}` had no Admin bypass
The query is marked `IUserScopedRequest`, so `AccessGuardBehavior.EnsureUserAsync` permitted only
**yourself or someone who shares an organization with you**. The seeded platform admin belongs to no
organization — so `GET /user` (AdminOnly) handed it a list it could not open.
**Fixed:** `ICurrentUserService` gained `IsAdmin` (from the JWT role claim, never throws), and
`EnsureUserAsync` short-circuits for a platform admin. Scoped deliberately to **user profiles only** —
it is not a skeleton key for organization data. Verified: admin → `GET /user/2` = **200** (was 403).
**The UI drawer was already built and needed no change.**

### 4.3 ✅ RESOLVED (Phase 10, 2026-07-26) — organization update/delete had no authorization
`UpdateOrganizationCommand` / `DeleteOrganizationCommand` carried no scoped-request markers and their
handlers checked only existence. **Any authenticated user could rename or delete any organization by
id.**
**Fixed:** new `IOrganizationAccessGuard.EnsureOrganizationOwnerAsync` — deliberately stricter than
`EnsureOrganizationAsync`, which permits active *members* and is far too weak for renaming or deleting
a whole workspace. Both handlers call it first. Verified: a non-owner gets **403
`NOT_ORGANIZATION_OWNER`** on both; the owner still gets 204.

### 4.3b ✅ RESOLVED (Phase 10, 2026-07-26) — 🚨 member commands had no authorization either
**Found while fixing §4.3; in no backlog before then.** All four `OrganizationMember` handlers —
`RemoveMember`, `DeactivateMember`, `ActivateMember`, `ChangeMemberRole` — loaded by id and mutated
with **no ownership, org or permission check**. Any authenticated user could deactivate, remove or
re-role any member of any organization. Same family as §4.1b and §4.3.
**Fixed:** all four now call `EnsurePermissionAsync(ManageMembers)` (the owner bypasses implicitly).
`ChangeMemberRole` additionally validated only that the role *exists*, not that it belonged to the
**same organization** — a cross-org role id silently imported another org's permission set; now 409
`ROLE_ORGANIZATION_MISMATCH`. Verified: all four → **403** for a non-member.

### 4.4 ✅ RESOLVED (Phase 10, 2026-07-26) — `RemoveMember` and `DeactivateMember` were identical
Both handlers called `member.Deactivate()`, so a "removed" member stayed listed as *Inactive*.
**Fixed:** `RemoveMember` now calls `IOrganizationMemberRepository.Remove()` (soft delete; the global
query filter hides the row), while `DeactivateMember` keeps the reversible toggle. Verified live: after
Remove the member **vanishes** from the list (2 → 1); after Deactivate they **stay listed** as Inactive.
**Frontend follow-up:** the members-page confirm copy currently says "will be marked inactive" as an
honest workaround — it can go back to describing a real removal (frontend Phase 26.2).

### 4.5 ✅ RESOLVED (Phase 9.B, 2026-07-26) — domain exceptions returned 500
`ExceptionHandlingMiddleware` now maps `ArgumentException` / `InvalidOperationException` to **400**
(`DOMAIN_RULE_VIOLATION`, `BusinessRuleViolation`) and logs a warning instead of an error. Verified:
assigning a personal task returns **400 `TASK_NOT_ASSIGNABLE`**, not 500.

---

## 5. What each side can do next — without waiting for the other

### Frontend, unblocked today
| Item | Why it needs no API change |
|---|---|
| **CSV / PDF report export** | The reports page already holds every figure; this is serialisation + download. **Highest value** — and it clears an item off the *backend's* backlog |
| **Calendar page** | Still `<a class="disabled" title="Coming soon">` in the org sidebar. Tasks already carry `startDate` + `expectedCompletionDate` |
| **Richer member trend charts** | `/report/member/{id}` already accepts `?from&to` — a trend is that call looped |
| **E2E tests** (Playwright) | 164 unit specs, zero E2E |
| Manager-role portal decision | The role exists in the API; which portal a Manager lands in is an unmade **product** call |

### Backend — ✅ every blocking item is done (Phases 10–13, 2026-07-26)
| Item | Status |
|---|---|
| §4.3 org update/delete authorization | ✅ Phase 10 |
| §4.3b member-command authorization (**found mid-work**) | ✅ Phase 10 |
| §4.2 Admin bypass in `EnsureUserAsync` | ✅ Phase 10 |
| §4.4 remove vs deactivate | ✅ Phase 10 |
| §3.0 task ↔ team link (`Task.TeamId` + `GetTeamTasks`) | ✅ Phase 11 |
| §3.0 role-filtered assignment | ✅ Phase 11 |
| §3.0 priority breakdown / project timeline / per-team task lists | ✅ Phase 12 |
| Admin org list + platform settings | ✅ Phase 13 |

**Remaining backend backlog — none of it blocks the frontend:** automated tests (still zero),
pagination on list endpoints, `ApiResponse<T>` envelope consistency, forgot-password endpoints,
Docker/CI.

**The whole remaining project is frontend work.** Start with V1-GAPS §3 (CSV export, calendar, trend
charts — never needed the API), then frontend Phases 26–29 to surface the 6 new endpoints.

---

## 6. Shared backlog (neither side started; not committed to a phase)

Due-date reminders + notifications · task comments & attachments · activity feed / audit trail ·
tags, labels, search, kanban board · recurring tasks · Redis caching · Hangfire jobs · event bus ·
Docker + CI/CD.

---

## 7. Changelog

| Date | Side | Change |
|---|---|---|
| 2026-07-26 | UI | **Frontend Phases 26–29 shipped — the two sides are in sync.** Consumed all six endpoints backend Phases 11–13 added: **Admin → Organizations** and **Admin → Platform Settings** pages (Phase 27), **team-scoped tasks + role/active-filtered assignment** (Phase 28), and **priority breakdown + project timeline + per-team task drill-down** in reporting (Phase 29). Phase 26 deleted the admin drawer's now-dead "denied state" and restored the honest member-removal copy. Endpoints consumed **74 → 80 of 82** (re-derived; the 2 left are the standing deliberate skips). Tests **179 → 195**. |
| 2026-07-26 | UI | ⚠️ **Maintenance mode exposed a two-request lie in sign-in.** `POST /auth/login` is exempt from the maintenance middleware but the following `GET /user/me` is not — so a non-admin authenticated **successfully** and was then shown *"Check your credentials and try again."* Fixed in `AuthFacade`. Also: a 503 `MAINTENANCE_MODE` now raises a sticky banner at the app root instead of one toast per failed request, since it describes a platform state rather than a request failure. **No API change needed.** |
| 2026-07-26 | API | **Phases 10–13 shipped — the backend is feature-complete.** Organization, Reporting and Admin all reach **100%**. Endpoints **76 → 82**; one additive migration (`AddTaskTeamAndPlatformSettings`). Closes §4.2, §4.3, §4.4 and the newly-found §4.3b. Adds `Task.TeamId` + `GET /team/{id}/tasks`, role/active filters on the member list, priority breakdown + project timeline + per-team task lists in reports, and `GET /admin/organizations` + `GET`/`PUT /admin/settings`. **Every remaining item in this project is now frontend work.** |
| 2026-07-26 | API | 🚨 **§4.3b found while fixing §4.3:** all four `OrganizationMember` command handlers (`Remove`/`Deactivate`/`Activate`/`ChangeRole`) had **no authorization whatsoever** — any authenticated user could act on any organization's members. Fixed in the same phase. `ChangeMemberRole` also accepted a role from a *different* organization. **In no backlog before now.** |
| 2026-07-26 | **Both** | **The Individual account is complete end to end.** API: §4.6 task reopen, §4.7 email verification, and a `TaskWorkLogs.Notes` NOT-NULL bug found by the new UI (migration `MakeWorkLogNotesNullable`). UI: the whole **Member portal** — My tasks (filters/paging/drawers/subtasks/time), a real dashboard on `GET /report/me`, and the verify-email page. Endpoints **73 → 76**, consumed **68 → 74**. Tests **164 → 179**. |
| 2026-07-26 | API | ⚠️ **Post-Phase-9 audit found two Individual gaps:** §4.6 no task reopen (in no backlog before now) and §4.7 a new account can't log in (no email-verification endpoint). Individual coverage corrected **~100% → ~85%**. |
| 2026-07-26 | API | **Phase 9 shipped — the Individual account has a workspace.** `POST /task/personal` + `GET /report/me`; endpoints **71 → 73**. Resolves §4.1, §4.1b and §4.5. No migration. Org flows regression-tested unchanged. **The Member portal is now the frontend's biggest gap.** |
| 2026-07-26 | API | **Phase 9 planned** (personal workspace for the Individual account) — see PHASES.md. Survey found Domain/DB/repo/guard/read-queries already support it; **no migration needed**. `OVERVIEW.md` corrected to stop claiming the vision is complete. |
| 2026-07-26 | API | 🚨 **§4.1b found:** `DeleteTask`, `StartTask`, `CompleteTask`, `CreateSubTask` and `StartWorkLog` handlers have **no authorization whatsoever**. Scheduled as Phase 9.0, ahead of the feature. |
| 2026-07-26 | UI | **v1 complete (Phase 22).** Dead code removed, skeletons + list pattern on every page, all forms on the validation engine, lint clean (0 errors), 164/164 tests. `docs/V1-GAPS.md` added. |
| 2026-07-26 | UI | Phases 17–21: full edit/delete, invitation flow both sides, org settings, admin user drawer + subtask rename, a11y pass (AA contrast enforced). |
| 2026-07-26 | **Both** | §3.0 added — `OVERVIEW.md` audited as a spec. Found **5 vision features in no other backlog**: task↔team link, role-filtered assignment, priority breakdown, project timeline, "which tasks" per team. All backend-first. |
| 2026-07-26 | **Both** | This ledger created. Counts re-derived from source: **71 endpoints exposed, 68 consumed.** |
| 2026-07-23 | API | IDOR fix (`AccessGuardBehavior` + scoped-request markers); `[Authorize]` on every controller. |
| 2026-07-23 | API | Phases 1–8 complete — write side, security, account types, Dapper read side, teams, permissions, time tracking, reporting. |

---

## 8. Where the detail lives

| Question | Read |
|---|---|
| How does the API work? | `TaskFlow/docs/` — `OVERVIEW` `ARCHITECTURE` `CONVENTIONS` `PHASES` `SESSIONS` |
| How does the UI work? | `TaskFlowUI/TaskFlowApp/docs/` — same five, plus `DESIGN` and `ATOMIC-DESIGN-GUIDE` |
| What does UI v1 deliberately exclude? | `TaskFlowUI/TaskFlowApp/docs/V1-GAPS.md` |
| **Are the two sides in sync?** | **This file.** |
