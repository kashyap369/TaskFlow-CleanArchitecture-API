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
> **Last verified implementation:** 2026-09-04, Meetings Phase 7 package P7.1 — ready-anytime meeting
> creation fixed and an AdminOnly meeting-readiness report added so a deployment that never received
> its LiveKit configuration is visible before a member is refused at join. Meetings remains
> owner-deferred and its organization-sidebar entry stays hidden.
> Planner Phases 17–23 are complete; see [PLANNER.md](PLANNER.md). The API exposes 180 endpoints;
> the UI consumes 177. The remaining three are the signed provider webhook and the two long-standing
> deliberate skips below.

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
   `src/app/features/*/*.repository.ts`, `core/meetings/meeting-collaboration.repository.ts`, **plus**
   `core/interceptors/refresh.interceptor.ts`
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
| **Version status** | **Pre-Planner product complete; Planner Phases 17–23 and Meetings Phase 6 implemented, certification pending** | **All three portals complete; Planner Phases 17–23 and Meetings Phase 6 implemented, certification pending** |
| **Endpoints** | **180** exposed (19 controllers; excludes the Angular-template `WeatherForecastController`) | **177 consumed.** The provider webhook is server-to-server; the 2 deliberate skips below remain |
| **Quality gates** | `dotnet test` ✅ **71/71** · build ✅ · EF model drift ✅ | `ng lint` **0 errors** · production build ✅ · **284/284** browser specs · WCAG AA on all 42 token pairings |
| **Biggest gap** | **Docker/staging Egress proof, legal approval and production LiveKit enablement** | **Ready-anytime validator fix plus staged two-client recording/archive proof** |
| **Vision coverage** (§3.0) | **Organization 100% · Reporting 100% · Individual 100%** | **All three portals complete** — Organization, Individual and Admin |

> **2026-07-26 — the two sides are now in sync.** Backend Phases 10–13 closed every ⛔/⚠️/⬜ row, and
> frontend Phases 26–29 consumed the six endpoints they added. **What remains on either side is
> optional polish, not parity:** V1-GAPS §3 on the UI.

### The 2 long-standing unconsumed endpoints — deliberate, not a gap

| Endpoint | Why unconsumed |
|---|---|
| `GET /task/mine` | Tasks *assigned* to the caller across organizations. The org portal already lists org tasks and the member portal lists personal ones; a third combined view isn't in the product description. |
| `GET /worklog/mine` | All the caller's work logs. The time drawer shows per-task logs and `/report/me` gives the totals, so this would be a third rendering of the same data. **Note: it requires `?from&to`** — omitting them binds `0001-01-01` and returns `[]`. |

Both are live and correct; nothing is blocked on them.

---

## 3. Feature parity matrix

**Organization Meetings roadmap**

| Milestone | API/infrastructure | UI | Reality |
|---|:--:|:--:|---|
| Phase 0 — LiveKit feasibility | ✅ | ✅ | Pinned provider stack and SDKs, provider-neutral backend boundary, scoped-token and signed/idempotent-webhook proofs, plus a development-only two-browser mic/camera/screen-share/reconnect harness. No production route or schema change. |
| Phase 1 — authoritative meeting core | ✅ | — | Aggregate/lifecycle, additive migration, three permissions and 13 feature-gated management/metadata routes. Real PostgreSQL coverage proves participant access and organization isolation. |
| Phase 2 — management and scheduling | ✅ | ✅ | Lazy list/detail, create/edit/lifecycle and registered-participant UI consume nine routes; scheduled/live meetings derive once into Calendar. |
| Phase 3 — secure guest access | ✅ | ✅ | Hash-only private/reusable links, meeting OTPs, opaque guest sessions, organizer decisions and a public lobby. All 19 meeting routes are bound. |
| Phase 4 — custom LiveKit room | ✅ | ✅ | Least-privilege member/guest grants, custom pre-join/room media UX, capability-aware moderation, signed replay-safe durable attendance, disposable LiveKit health, and two-client presence/leave/reconnect evidence are complete. |
| Phase 5 — collaboration and archive | ✅ | ✅ | Idempotent persisted chat, optimistic shared-note revisions, private scanned files, member/guest capability parity, complete ordered archives and storage-safe retention cleanup. |
| Phase 6 — recording, Egress and playback | 🟡 | 🟡 | Application/UI and local Egress configuration are complete. Real staging playable-MP4/capacity evidence and jurisdiction-specific legal/product approval still gate completion and production enablement. |
| Phase 7 — hardening and rollout | 🟡 | 🟡 | **Deferral lifted 2026-09-04; sidebar entry restored.** Production now carries a complete valid LiveKit configuration and the media endpoint answers healthy over TLS; a real call is still unproven. **P7.1 done:** ready-anytime creation fixed, and `GET /admin/meetings/readiness` plus its admin panel make an unpropagated `LiveKit__*` deployment visible instead of surfacing as a join refusal. Six packages remain (threat model, capacity, telemetry, E2E, infrastructure, policy docs). |

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
| **Meetings readiness (admin)** | `GET /admin/meetings/readiness` | ✅ | ✅ | ⬜ | **Meetings P7.1 (2026-09-04).** Reports what the running process loaded — flags, media scheme/host, credential presence, recording storage — and proves local join-token signing. No secret is returned (API key as an 8-char fingerprint, secret as a length). Rendered on `/admin/settings` with a re-check action. Not yet exercised against a deployed API |

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
| Projects CRUD + detail | 5 on `/project` | ✅ | ✅ | ✅ | Create requires `CreateProject`; update/delete require `ManageProjects`. Individual members can enter joined organization workspaces from their personal portal. |
| **Project plan import** | `POST /project/plan` | ✅ | ✅ | ⬜ | Landed 2026-09-03 in commits `d62ca61` (API) / `c3582ad` (UI) and **undocumented until 2026-09-04**. One transactional import of a whole project with tasks, subtasks, team and assignee resolution; the UI parses CSV in `@shared/utils/project-plan-csv` and offers it on both the organization and member projects pages |
| Tasks CRUD + detail | `POST` `PUT` `DELETE` `GET /{id}` | ✅ | ✅ | ✅ | `TaskListItem` has no `description` — edit fetches the detail first |
| Task lists (org / project) | `GET /task/organization/{id}`, `/project/{id}` | ✅ | ✅ | ✅ | |
| Task lifecycle | `PUT /{id}/start`, `/complete`, `/reopen` | ✅ | ✅ | ✅ | **Reopen added in Phase 9** — completes the documented Todo→InProgress→Completed→reopen cycle |
| Task assignment | `PUT /{id}/assign/{userId}`, `/unassign` | ✅ | ✅ | ✅ | Inline assignee `<select>` on every row |
| **Calendar task scheduling** | `PUT /task/{id}/schedule` | ✅ | ✅ | ⬜ | Phase 2: focused date-only command, `ManageTasks`, shared filters, details, authorized drag/resize and visible rollback; automated end-to-end gates pass, live API/browser mutation not rerun in this session |
| **Calendar-owned events** | `GET /calendar/organization/{id}`, `POST`/`PUT /calendar`, `DELETE /calendar/{id}` | ✅ | ✅ | ✅ | Phase 4: organization events, member leave, holidays, UTC/timezone-aware timed entries, all-day validation and bounded Daily/Weekly/Monthly recurrence under `ManageCalendar` |
| **Meeting lifecycle** | 7 on `/meeting` for create/list/detail/update/start/end/cancel | ✅ | ✅ | ⬜ | Phase 2: lazy list/detail, validated instant/scheduled create/edit and permission-aware start/end/cancel. Automated gates pass; a credentialed live mutation was not run. |
| **Meeting badges, participants and access links** | 7 on `/meeting/{id}` metadata routes | ✅ | ✅ | ⬜ | Badge creation, registered/guest participant decisions and private/reusable link list/create/rotate/revoke are surfaced. Raw tokens appear once. |
| **Public meeting guest access** | 5 on `/meeting/guest` | ✅ | ✅ | ⬜ | Fragment-only invite capture, email-code verification, optional exact-email account binding, opaque tab session restore, display-name confirmation and scoped lobby. Automated HTTP/browser gates pass; no production guest was created. |
| **Meeting collaboration and archive** | 18 registered/guest message, note, asset and archive routes | ✅ | ✅ | ✅ | Persist-first LiveKit announcements reconcile canonical ordered chat; notes use optimistic revisions; private scanned files enforce type/signature/size/quota and authorization; ended archives retain timing, attendance, authors/uploaders and delete storage before retention cleanup. |
| **Personal tasks** | `POST /task/personal`, `GET /task/mine/personal` | ✅ | ✅ | ✅ | Member portal **My tasks** page: filters, paging, create/edit drawer, lifecycle |
| **Personal projects** | `POST /project/personal`, `GET /project/mine/personal` + shared project CRUD | ✅ | ✅ | ✅ | Creator-only projects; My Tasks can filter/create within a private project |
| **Planner resources and secure files** | 8 on `/planner/projects/{id}/board/resources` | ✅ | ✅ | ✅ | Notes, links, documents, private authorized preview/download, update, unlink/relink, and delete; binaries stay outside scene JSON |
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
| Automated tests | ✅ **49 tests**, including disposable-PostgreSQL HTTP isolation coverage | ✅ 258 browser specs · ⬜ no E2E |
| Docker / CI-CD | ⬜ Backlog | ⬜ |

---

## 4. Open issues — the blocking list

### 4.0 ⚠️ OPEN (production audit, 2026-09-01) — Meetings media disabled and ready-anytime validation

Production meeting `#3` was created, assigned to Shubham Kashyap and started successfully; the newly
deployed Phase 5 collaboration surface also loaded. The room join-token request fails with
`LiveKit media is not enabled`, so the PC/mobile audio-video proof is blocked until a public trusted
`wss://` LiveKit deployment (with Redis/TURN as required) and environment-owned `LiveKit__Enabled`,
`LiveKit__Url`, `LiveKit__ApiKey` and `LiveKit__ApiSecret` values are configured. Separately, switching
the creation form from scheduled to ready-anytime leaves hidden start/end required validators attached
and prevents submission. Scheduled creation works. Resolve and regression-test both in Meetings Phase 7.

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
| **Calendar page** | ✅ Phases 1–4 implemented: Schedule + Project Timeline, shared filters/details, authorized rescheduling, server-computed Team Capacity, and calendar-owned events/leave/holidays with recurrence. Optional booking Phase 5 remains a product decision. |
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
pagination on list endpoints, `ApiResponse<T>` envelope consistency, Docker/CI.

**The whole remaining project is frontend work.** Start with V1-GAPS §3 (CSV export, calendar, trend
charts — never needed the API), then frontend Phases 26–29 to surface the 6 new endpoints.

---

## 6. Shared backlog (neither side started; not committed to a phase)

Due-date reminders + notifications · task comments & attachments · activity feed / audit trail ·
tags, labels, search, kanban board · recurring tasks · Redis caching · Hangfire jobs · event bus ·
Docker + CI/CD.

> Planner is no longer part of the uncommitted backlog. Its product contract and cross-project roadmap
> are committed in [PLANNER.md](PLANNER.md) and [PHASES.md](PHASES.md) Phases 17–23.

---

## 7. Changelog

| Date | Side | Change |
|---|---|---|
| 2026-09-05 | **API** | **Meetings Phase 7 package P7.2 — threat model and abuse review.** Reviewed the whole meeting attack surface against the code and recorded it in [MEETINGS-THREAT-MODEL.md](MEETINGS-THREAT-MODEL.md), then fixed the eight defects found. The serious two: revoking a leaked access link evicted nobody, because guest sessions carried no reference to the link they were exchanged for (fixed by the additive `AddMeetingGuestSessionAccessLink` migration; revoke and rotate now kill those sessions and eject the guests); and recording consent was collected from webhook-written attendance rather than the provider's live roster, so a delayed webhook could produce a recording nobody was asked about (the roster is now authoritative, and an unreadable roster refuses the request). Also fixed: recording veto by a participant never asked, a removed guest renaming themselves, cross-meeting chat replies, a 500 on a soft-deleted participant's session, an unrated anonymous webhook endpoint, and rotation minting an already-expired link. Nine residual risks are accepted with reasons, five of them owner decisions. Routes unchanged at **180 total / 177 consumed**; backend tests **82/82**, build and EF drift pass. **No frontend change was required** — the Angular client renders server messages and branches on no meeting error code. |
| 2026-09-04 | **Both** | **First working production meeting — two devices, real audio/video/screen share over `wss://livekit.inksphere.space`.** Three faults stood between the code and a call: Dokploy `v0.29.14` never injected the API service environment into its container (proved with `env` inside the container; fixed with `docker service update --env-add`, and it can be wiped by any redeploy); the Angular room aborted a fully connected call when Android Chrome rejected the `setSinkId` speaker preference, killing every mobile session at ~3s; and `room_finished` then archived the meeting nobody had attended. Join preferences are now non-fatal, and auto-end requires one attendance interval of at least `Meetings:AutoEndMinimumSessionSeconds` (default 30). Recording remains off — production had no Egress worker, now defined in `infra/meetings/dokploy.compose.yml` with `infra/meetings/RECORDING.md` covering storage, capacity and the open legal/consent gate. Backend **73/73**, frontend **286/286**; builds, lint, design lint, 42 contrast checks and EF drift pass. No migration; no endpoint change. |
| 2026-09-04 | **Both** | **Meetings deferral lifted; the organization sidebar entry is restored** (an exact revert of the frontend's `fc84119`). Verified in Dokploy that the production API service carries all eleven `LiveKit__*` / `Meetings__*` variables with `LiveKit__Enabled=true`, a trusted `wss://` URL, an API secret comfortably over the 32-character minimum and a webhook tolerance inside the accepted range; `https://livekit.inksphere.space` answers `200 OK` over TLS. `Meetings__RecordingEnabled` stays `false` pending the legal/retention decision. **A production multi-client call is still unproven** — confirm the readiness panel reads Ready on the deployed API, then run a two-device call. |
| 2026-09-04 | **Both** | **Meetings Phase 7 package P7.1.** Fixed ready-anytime meeting creation: the drawer's start/end inputs carried the template `required` attribute, so Angular's `RequiredValidator` stayed attached to the controls after `@if` destroyed the inputs, leaving the form invalid with nothing on screen to explain it. It is now `aria-required`, with completeness still enforced in `submit()` and a regression spec that renders the template. Added `GET /admin/meetings/readiness` (AdminOnly) and its admin Platform-settings panel so an operator can see whether the running service actually received its `LiveKit__*` configuration — the deployment failure that deferred Meetings on 2026-09-02 — proving local join-token signing without ever returning a credential. Routes re-derived at **180 total / 177 consumed**; backend tests **71/71**, frontend specs **284/284**, builds, lint, design lint, 42 contrast checks and EF drift all pass. No migration. |
| 2026-09-04 | **Both** | **Repaired two quality-gate breaks and one documentation gap left by the 2026-09-03 project-plan-import commits.** `ng lint` was failing on a real `shared → feature` boundary violation (the plan contracts now live in `shared/models/project-plan.model.ts`, re-exported from `organization.models.ts`, matching how invitations are shared between portals) plus two useless regex escapes; `design:lint` was failing on a non-token `border-radius`. The import feature itself was in no doc — it now has a parity row above and the changelog row below. |
| 2026-09-03 | **Both** | **Transactional project plan import** (`d62ca61` API, `c3582ad` UI) — recorded retroactively on 2026-09-04. `POST /project/plan` creates a project with its tasks, subtasks, team and assignee resolution in one transaction (`IUnitOfWork` gained explicit transaction support); the UI adds a shared CSV parser and an import flow on both the organization and member projects pages. |
| 2026-09-02 | **Both** | **Organization Meetings deferred by owner.** The implemented API/UI and data are retained, but the organization-sidebar entry is hidden so the feature is not presented as production-ready. Dokploy `v0.29.14` saved the LiveKit settings without propagating them into the API Swarm service; resume only after that boundary is fixed and a real multi-client production call passes. No endpoint count changed. |
| 2026-09-01 | **Both** | **Organization Meetings Phase 6 implementation complete; external certification pending.** Added immutable current-participant consent, late-join gates, host-only recording lifecycle, LiveKit room-composite MP4 Egress, webhook/recovery reconciliation, private member/guest playback and storage-first deletion. Angular binds all nine routes with a persistent recording indicator and archive controls. Routes are **178 total / 175 consumed**; backend tests pass **65/65**, frontend specs **278/278**, and builds, lint/design lint, 42 contrast checks and EF drift pass. Docker is unavailable locally, so a real playable archive/capacity run and jurisdiction-specific legal/product approval still gate Phase 6 completion and production recording. |
| 2026-09-01 | **Both** | **Production Meetings verification follow-up.** Backend Phase 5 commit `d397ea5` was pushed and deployed; production meeting `#3` was created, assigned to Shubham Kashyap and started, and the collaboration panel loaded. Media remains blocked because production reports `LiveKit media is not enabled`; ready-anytime creation is also blocked by stale hidden date validators after schedule removal. Scheduled creation works. Both items are now explicit Phase 7 rollout/hardening work. |
| 2026-09-01 | **Both** | **Completed Organization Meetings Phase 5.** Added idempotent durable chat, optimistic shared-note revisions, private scanned files, ordered timing/attendance/content archives and storage-first retention cleanup through `AddMeetingCollaborationArchive`. Angular binds all 18 member/guest collaboration routes and uses persist-first LiveKit announcements only for reconciliation. Routes are **169 total / 166 consumed**; the only unconsumed routes remain the server-to-server webhook and two deliberate skips. Disposable PostgreSQL proves retry deduplication, stale-note conflict, outsider asset denial, scanned upload/download and ended archive reconstruction. Backend tests pass **62/62**, frontend specs **276/276**, and builds, lint/design lint, 42 contrast checks and EF drift pass. Phase 6 is READY. |
| 2026-08-31 | **Both** | **Completed Organization Meetings Phase 4.** Added least-privilege registered/guest join grants, a custom responsive pre-join and room experience, capability-aware host/co-host mute/remove, signed raw-body LiveKit webhooks, connection-scoped attendance and durable replay receipts through `AddMeetingWebhookReceipts`. Official LiveKit Server `1.13.6` passed the standalone disposable health path; two independent browser contexts proved registered/guest presence, leave and fresh-token reconnect with real webhook delivery. Routes are **151 total / 148 consumed**; the only unconsumed routes are the server-to-server webhook and two deliberate skips. Backend tests pass **60/60**, frontend specs **275/275**, and builds, lint/design lint, 42 contrast checks and EF drift pass. Phase 5 is READY. |
| 2026-08-31 | API | **Advanced Organization Meetings Phase 4 P4.1.** Stabilized the meeting integration assertion and added disposable-PostgreSQL HTTP coverage for assigned-member token issuance, unassigned-user denial, and verified-guest denial until organizer admission. The test host signs credentials using test-only LiveKit settings without contacting a media server and assigns each client a distinct forwarded test IP, preserving real guest rate limits without cross-test contention. No endpoint or UI-count change; backend tests pass **59/59**. P4.2 moderation and attendance is next. |
| 2026-08-31 | **Both** | **Completed Organization Meetings Phase 3.** Added hash-only private/reusable invitations, rotation/revocation, invitation and OTP mail, stable guest participants, exact-email optional account binding, opaque one-meeting guest sessions and audited organizer admit/deny/revoke/remove decisions. Angular now manages badges/links and provides a fragment-scrubbing public verification/lobby flow outside protected layouts. Routes are **144 total / 142 consumed**; all 19 meeting routes are bound. Backend tests pass **52/52**, frontend specs **267/267**, and production build, lint/design lint, detector, 42 contrast checks, responsive browser QA and EF drift pass. Phase 4 is READY. |
| 2026-08-30 | UI | **Completed Organization Meetings Phase 2.** Added lazy Meetings navigation/list/detail, Upcoming/Live/Past search/filter/paging, validated instant/scheduled create/edit, lifecycle actions, registered-participant access and truthful later-phase placeholders. Calendar derives timed meetings without duplicate rows. Nine meeting routes are now bound (**132/138 consumed**); the four badge/link metadata routes stay staged for Phase 3. All **262/262** specs, production build, lint/design lint, detector and all 42 contrast checks pass. |
| 2026-08-30 | API | **Completed Organization Meetings Phase 1.** Added the meeting lifecycle aggregate with badge/participant/hash-only access-link/attendance foundations, additive `AddMeetingCore` migration, `CreateMeetings`/`ManageMeetings`/`RecordMeetings`, validated staged feature configuration, and 13 authenticated management/metadata routes. Raw access tokens are returned once and never exposed by later reads. Routes are now **138 total / 123 consumed**; backend build and all **49/49** tests pass, disposable PostgreSQL proves participant access plus outsider/cross-organization denial, and EF reports no drift. Phase 2 is READY. |
| 2026-08-30 | **Both** | **Completed Organization Meetings Phase 0.** Pinned LiveKit Server `1.13.6`, .NET server SDK `1.2.3`, JavaScript client `2.22.1` and Redis `8.2.9-alpine`; added the provider-neutral media boundary, local Compose/config, five-minute scoped-token proof and signed/idempotent-webhook proof. A development-only Angular harness verified two isolated browser contexts exchanging microphone/camera, screen sharing and fresh-token reconnect with real webhook delivery. Production endpoint counts remain **125 / 123**; backend tests pass 42/42, frontend specs 258/258, builds/lint/design/contrast and EF drift pass. Phase 1 is READY. |
| 2026-08-29 | **Both** | **Completed Organization Calendar Phase 4.** Added the `CalendarEntry` aggregate and `AddCalendarEntries` migration, new `ManageCalendar` permission, four organization-scoped CRUD/window routes, timezone/all-day validation and bounded Daily/Weekly/Monthly recurrence expansion. Angular merges occurrences through the existing Schedule adapter and adds accessible permission-aware create/edit/delete management. Routes are now **125 total / 123 consumed**; frontend specs pass 256/256, backend tests pass 40/40 with real HTTP/PostgreSQL recurrence/isolation/delete coverage, and build/lint/design lint/contrast/EF drift pass. |
| 2026-08-29 | **Both** | **Completed Organization Calendar Phase 3.** Added nullable task estimates and member weekly capacities through migration `AddCalendarCapacity`, focused `ManageTasks`/`ManageMembers` writes, and an organization-scoped Monday-based capacity query. Angular adds a responsive six-week Team Capacity grid plus in-context hours/estimate editing; missing data is always `NotEnoughData`, never partial availability. Routes are now **121 total / 119 consumed**; frontend specs pass 254/254, backend tests pass 35/35 including real UTC/isolation PostgreSQL coverage, and build/lint/design lint/contrast/EF drift pass. |
| 2026-08-29 | **Both** | **Completed Organization Calendar Phase 2.** Added persistent shared filters and a reusable accessible detail drawer, plus authorized drag/resize in FullCalendar and Frappe Gantt. The new focused `PUT /task/{id}/schedule` route updates dates only, validates the window and reuses `ManageTasks`; failed moves restore server state visibly and project bars remain immutable. Routes are now **118 total / 116 consumed**; frontend specs pass 253/253, backend tests pass 30/30, and build/lint/design lint/contrast pass. |
| 2026-08-29 | UI | **Completed Organization Calendar Phase 1.** Enabled `/organization/calendar` and its sidebar item with FullCalendar month/week/list Schedule views plus a lazy, project-selectable, read-only Frappe Gantt timeline. TaskFlow-owned adapters derive dates, status, progress, assignee and team context from the existing organization DTOs; no endpoint, schema or migration changed. The full frontend suite passes 247/247, build/lint/design lint/contrast pass, and desktop/mobile browser verification covers both views and item selection. |
| 2026-08-28 | **Both** | **Completed Planner Phase 23 and production rollout.** Added bounded/malicious scene validation, root-only persistence, indexed rolling revision retention, upload signature validation and private headers, Planner/upload rate limits, feature-flag rollback, traces/metrics/structured audit logs, coalesced large-canvas serialization, and explicit legacy browser-scene import with rollback preservation. Backend tests are 27/27 and frontend browser specs are 240/240; builds, lint/design lint, Storybook, EF drift, performance, ownership/security integration coverage, Dokploy deployments, and live health checks pass. |
| 2026-08-28 | **Both** | **Completed Planner Phase 22.** Added immutable transactional primary baselines and ordered snapshots, persistence-boundary scope auditing with actor/time/reason, New/Changed/Removed history, five owner-authorized APIs, and Angular finalization/history/filter/field-comparison UX. Progress-only status/completion/time-log changes are excluded. API routes are now **117 total / 115 consumed**; backend tests are 22/22 and frontend tests are 236/236, with builds, lint/design lint, and EF drift green. |
| 2026-08-28 | **Both** | **Completed Planner Phase 21.** Added owner-scoped Note/Link/Document resources, private object-storage assets, migration `AddPlannerResourcesAndAssets`, 25 MB/type/name validation, SHA-256, scan-status hook, authorized preview/download, metadata updates, unlink/relink retention, and explicit object deletion. Angular adds resource cards, creation/inspector flows, previews/downloads, and an unlinked-resource library. API routes are now **112 total / 110 consumed**; backend tests are 21/21 and frontend tests are 234/234, with production build, lint/design lint, and EF checks green. |
| 2026-08-28 | **Both** | **Completed Planner Phase 20.** Added fixed validated template types, AdminOnly Draft/Published/Archived management, immutable published versions, node version snapshots, and migration `AddPlannerTemplateLibrary`. Angular adds an admin library and member Planner picker applying published defaults/colors/dimensions; archived templates leave old cards renderable. API routes are now **104 total / 102 consumed**; backend tests are 18/18 and frontend tests are 232/232, with build, lint/design lint, Storybook, detector, and EF checks green. |
| 2026-08-28 | **Both** | **Completed Planner Phase 19.** Added stable server-owned links from Excalidraw elements to canonical Project/Task/Subtask records, six owner-authorized node/workspace routes, backend-derived progress and rehydration, atomic Planner-aware create/edit flows, and explicit unlink-versus-delete semantics. Projects gained problem statement, budget/currency, and approximate duration fields through migration. The Angular canvas now creates linked cards, refreshes live labels, restores missing linked cards, and edits them through a responsive inspector. API routes are now **98 total / 96 consumed**; backend tests are 15/15 and frontend tests are 231/231, with build, lint/design lint, Storybook, detector, and EF checks green. |
| 2026-08-28 | **Both** | **Completed Planner Phase 18.** Added owner-authorized primary boards, immutable revisions, stable node records, migration backfill, cloud load/save/history APIs with ETags and database-safe optimistic concurrency, and Angular debounced autosave with ordered IndexedDB recovery and explicit offline/failure/conflict resolution. Disposable-PostgreSQL HTTP tests prove ownership isolation, cross-device restore, stale-tab rejection, and simultaneous-write safety. API routes are now **92 total / 90 consumed**; backend tests are 12/12 and frontend tests are 230/230, with build, lint/design lint, Storybook, and EF model checks green. |
| 2026-08-28 | UI | **Completed Planner Phase 17.** `/member/planner` now bypasses the normal member content shell and provides a true `100dvw × 100dvh` Excalidraw workspace with compact project/progress/save/tool overlays, creator-owned project loading and switching, remembered last project, robust empty/loading/error states, and inline personal-project creation. Temporary browser scene storage is separated by user and project; cloud authority intentionally begins in Phase 18. Desktop/mobile component previews, Angular build, lint/design lint, and Storybook build pass; focused Jasmine specs compile but the host ChromeHeadless GPU sandbox crashes before execution. No API endpoint or schema change. |
| 2026-08-27 | **Both** | **Committed the Planner end-to-end specification and Phases 17–23.** The target is a full-viewport, project-scoped Excalidraw workspace with cloud scene persistence/concurrency, canonical project/task/subtask links, admin-versioned templates, secure notes/documents/media, immutable primary requirement baselines, New/Changed/Removed history, comparison, and production hardening. At this roadmap checkpoint implementation had not started; Phase 17 landed in the following implementation session. See `docs/PLANNER.md`. |
| 2026-08-15 | **Both** | **Added creator-only personal projects and the initial Excalidraw Planner.** Personal tasks can belong to a private project owned by the same creator; joining an organization never exposes or mixes that data. Added two API routes (**88 total / 86 consumed**), applied the nullable-organization migration, and verified cross-user reads return 403. Planner autosaves per user in that browser; server-side Planner persistence remains intentionally deferred. |
| 2026-08-15 | **Both** | **Closed project-write authorization and completed Individual membership UX.** `CreateProject` now protects creation and `ManageProjects` protects update/delete through the standard organization permission checker. Individual accounts keep the personal portal as home but can enter organizations they own or joined, switch back to personal work, and see new memberships immediately after accepting invitations. No endpoints or migration added. Backend build and frontend lint/build pass; 16 focused browser regressions are green. |
| 2026-08-14 | **Both** | **Account recovery and passwordless sign-in shipped end to end.** Four auth endpoints add generic, rate-limited code requests plus reset/login verification; persisted codes are hashed, expiring, single-use and attempt-limited. Password reset revokes active refresh tokens. Angular adds email-code mode to personal/organization/admin sign-in and a responsive forgot-password flow. Endpoints **82 → 86**, consumed **80 → 84**. |
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
| What is the complete Planner product and architecture contract? | `TaskFlow/docs/PLANNER.md` |
| **Are the two sides in sync?** | **This file.** |
