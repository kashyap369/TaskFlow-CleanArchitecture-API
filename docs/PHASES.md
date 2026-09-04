# TaskFlow — Phases & Status

> Keep the Current Status section up to date at the end of every session.

## 🟡 Organization Meetings Phase 7 — P7.1 done: readiness and ready-anytime creation (2026-09-04)

The ready-anytime creation bug is fixed and the deployment blindness behind the 2026-09-02 deferral is
now observable. `GET /admin/meetings/readiness` (AdminOnly) reports what the running process actually
loaded — feature flags, media URL scheme/host, whether the key/secret arrived, recording storage — and
proves local join-token signing, with plain-language blockers. It never returns a credential: the API
key appears as an eight-character fingerprint and the secret only as a length. Backend `71/71`,
frontend `284/284`, builds, lint, design lint, 42 contrast checks and EF drift all pass; no migration.
Six Phase 7 packages remain (threat model, capacity, telemetry, E2E, infrastructure, policy docs), and
Meetings stays owner-deferred with its sidebar entry hidden.

## ▶️ Organization Meetings resumed (2026-09-04)

The owner lifted the 2026-09-02 deferral and the organization sidebar entry is restored. The
production configuration that caused the deferral is now correct and complete: the API service
carries all eleven `LiveKit__*` / `Meetings__*` variables, `LiveKit__Enabled=true`, the URL is a
trusted `wss://` endpoint, and that endpoint answers a healthy `200 OK` over TLS. Recording stays
off (`Meetings__RecordingEnabled=false`) pending the legal/retention decision.

**Still unproven:** no production multi-client audio/video call has succeeded yet. Confirm
`/admin/settings` → Meetings readiness reads **Ready** on the deployed API, then run a real
two-device call before treating the feature as verified in production.

### Superseded — Organization Meetings deferred (2026-09-02)

The owner deferred the Meetings feature and the frontend sidebar entry was hidden. Existing meeting
code and data were retained for a later restart. Rollout was blocked until Dokploy reliably injected
the saved `LiveKit__*` runtime variables into the API Swarm service and a production multi-client
audio/video call was verified. All unrelated TaskFlow features remained in scope and available.

## 🟡 Organization Meetings Phase 6 — implementation complete, certification pending (2026-09-01)

Consent-aware recording is implemented end to end in the repositories: immutable current-participant
consent, late-join gating, host start/stop/end orchestration, pinned LiveKit Egress, replay-safe webhook
and recovery reconciliation, private member/guest playback, creator deletion and storage-first
retention. The additive recording migrations include a partial unique index preventing concurrent
active recordings. Backend build, all 65 tests and EF drift pass; the sibling Angular repository passes
all 278 specs, production build, lint/design lint and 42 contrast checks. Phase 6 remains IN PROGRESS
until a Docker-capable staging environment proves a playable room-composite MP4 and declared capacity,
and legal/product approves the target geography's disclosure, consent and retention policy.

## ⚠️ Organization Meetings production verification follow-up (2026-09-01)

Production successfully created meeting `#3`, assigned the Shubham Kashyap account, started the
meeting and loaded the deployed Phase 5 collaboration surface. Realtime media is not yet available:
the API reports `LiveKit media is not enabled`, leaving pre-join disabled until a public LiveKit/
Redis/TURN deployment and production credentials are configured. The UI also retains hidden start/end
required validators after switching from scheduled to ready-anytime, so only scheduled creation works.
These are Phase 7 rollout/hardening items; Phase 5 remains complete and Phase 6 certification remains pending.

## ✅ Organization Meetings Phase 5 — persistent collaboration and archive (2026-09-01)

Phases 0–5 are DONE and Phase 6 is READY. Added durable idempotent chat, one optimistic-versioned
shared note with immutable revisions, private scanned meeting files, the complete ordered archive and
six-hour retention cleanup through `AddMeetingCollaborationArchive`. The same capability and retention
rules govern registered and guest sessions; persist-first LiveKit announcements only trigger canonical
API reconciliation. All 18 new routes are bound by Angular. Disposable PostgreSQL proves retry
deduplication, note conflict, outsider denial, scanned upload/download, read-only ended state and
archive reconstruction. Backend build, all 62 tests and EF drift pass; frontend evidence is recorded
in the sibling repository.

## ✅ Organization Meetings Phase 4 — custom LiveKit room (2026-08-31)

Phases 0–4 are DONE and Phase 5 is READY. Added least-privilege registered/guest room credentials,
host/co-host mute and removal, signed raw-body webhook processing, connection-scoped attendance and
durable replay receipts through `AddMeetingWebhookReceipts`. Disposable PostgreSQL proves moderator
authorization, replay safety, removal disconnect/revocation and attendance persistence; official
LiveKit Server `1.13.6` passed its standalone health path and two independent browser contexts proved
registered/guest presence, leave and fresh-token reconnect with signed webhook delivery. Backend build,
all 60 tests and EF drift pass. The full room UI and frontend evidence are recorded in the sibling repo.

## ✅ Organization Meetings Phase 2 — management and scheduling UI (2026-08-30)

The approved cross-repository product, security, architecture and Phase 0–7 delivery contract is in
**[MEETINGS.md](MEETINGS.md)**. It covers registered and unregistered email participants, revocable
private/reusable links, separate capability levels and custom display badges, custom LiveKit calling,
persistent collaboration/attendance, consent-aware recording and production rollout.

Phases 0–2 are DONE and Phase 3 is READY. Angular now consumes the meeting list/detail,
create/update/lifecycle and registered-participant contract through lazy organization routes, with
permission-aware controls and organization-switch isolation. Scheduled/live meeting records derive
once into Calendar without duplicate persistence. Nine meeting routes are bound now; four badge/link
metadata routes remain staged for Phase 3 guest access. The frontend passes all 262 specs plus
build/lint/design/contrast/detector; the unchanged backend remains 49/49 with no EF drift.

## ✅ Calendar Phase 4 — Events, leave, holidays + recurrence (2026-08-29)

- Added the `CalendarEntry` aggregate, `ManageCalendar` permission and additive
  `AddCalendarEntries` migration for organization events, member leave and holidays.
- Four organization-scoped routes provide bounded window reads and permission-gated CRUD. Timed
  entries retain UTC boundaries plus timezone, leave/holidays are all-day, and recurrence is limited
  to Daily/Weekly/Monthly with interval and optional inclusive end date.
- Real HTTP/PostgreSQL coverage proves recurrence expansion, all-day member leave, outsider denial and
  independent soft deletion. The full backend suite passes 40/40 and EF has no model drift.

## ✅ Calendar Phase 3 — Estimates, member capacity + server totals (2026-08-29)

- Added nullable task estimate minutes and organization-member weekly capacity minutes through
  migration `AddCalendarCapacity` and focused `ManageTasks` / `ManageMembers` commands.
- Added an organization-scoped, Monday-based capacity query. PostgreSQL computes task totals,
  remaining minutes and Light/Balanced/Heavy state; missing capacity or estimates returns
  `NotEnoughData` instead of partial availability.
- Real HTTP/PostgreSQL coverage proves UTC Monday/Sunday edges, state thresholds, missing-estimate
  behavior and cross-organization denial. The full backend suite passes 35/35 and EF has no drift.

## ✅ Calendar Phase 2 — Focused task scheduling API (2026-08-29)

- Added `PUT /task/{taskId}/schedule` with a dedicated command/validator and `Task.Reschedule`; it
  changes only `StartDate`/`ExpectedCompletionDate` and rejects an end before the start.
- Organization access is checked before loading, personal tasks are rejected, and the existing
  `ManageTasks` permission (including the standard owner bypass) is the write authority.
- Three focused handler/validation tests cover authorized persistence, denied mutation and invalid
  windows. The complete backend suite passes 30/30 with the required test one-time-code secret set.

## ✅ PLANNER ROADMAP COMPLETE (2026-08-28)

Planner's canonical requirements and architecture are in **[PLANNER.md](PLANNER.md)**. Phases 17–23
now deliver the immersive shell, cloud persistence, linked work objects, immutable templates, secure
resources, immutable requirement baselines, and an observable feature-flagged production rollout.

## ✅ Phase 23 — Hardening, scale, and production rollout (2026-08-28)

**Outcome:** Planner is bounded, observable, secure, performant on realistic boards, and safely
deployable behind an explicit feature flag.

- Scene validation now caps UTF-8 payload size, JSON depth, strings, and elements; rejects embedded
  binary data and unsafe link schemes; and saves through a root-only board query instead of hydrating
  the complete node/resource graph.
- Revision history retains the latest 100 checkpoints with a board/time index and transactional pruning.
- Uploads combine size/type/name checks with file-signature validation, separate rate limiting, private
  cache/security headers, and the existing scan gate before content is served.
- Planner requests emit traces, duration/request/failure/conflict/mutation metrics, structured slow/error
  warnings, and mutation audit events without logging scene or file content.
- `Planner:Enabled` gates the server routes, Angular route, and navigation. Legacy browser-only scenes
  are imported only after explicit user action; the original copy is retained for rollback.

**Delivered evidence:** backend tests pass 27/27, frontend specs pass 240/240, production builds,
lint/design lint, Storybook, EF migration drift, security/ownership integration coverage, the 5,000-
element performance test, and critical Planner browser specs pass. Production deployment is recorded
in [ProjectCompletion.md](ProjectCompletion.md).

## ✅ Phase 22 — Primary requirements, changes, and comparison (2026-08-28)

**Outcome:** the initial plan is an immutable primary baseline and later scope changes are fully traceable.

- Added `RequirementBaseline`, `RequirementSnapshot`, and `RequirementChange` with migration
  `AddPlannerRequirementBaselines`; Baseline 1 captures project/task/subtask scope and ordering atomically.
- Added five owner-authorized APIs for finalization, baseline list/detail, append-only history, and
  field-level comparison with New/Changed/Removed filters and optional reasons.
- Requirement auditing runs at the EF persistence boundary, so normal project/task/subtask endpoints
  cannot bypass history. Scope fields are compared; status, completion, and work logs are excluded.
- Angular enables the Planner Requirements tool with irreversible-finalization guidance, immutable
  baseline inspection, working-change filters, reasons, and baseline/current field diffs.

**Delivered evidence:** real HTTP/PostgreSQL integration proves ownership, immutable finalization,
progress exclusion, additions, edits, removals, filters, and field comparison. Backend tests pass
22/22, frontend specs pass 236/236, both builds and lint pass, and EF reports no drift.

## ✅ Phase 17 — Immersive Planner shell and project context (2026-08-28)

**Outcome:** Excalidraw behaves like a real full-screen editor and every session has an explicit
personal-project context.

- Add a dedicated authenticated Planner layout at `100dvw × 100dvh`; remove member content max-width,
  outer padding/card treatment, document scroll, and the separate page heading from this route.
- Keep native Excalidraw controls and add compact overlays for return navigation, project selection,
  project progress, save state, library, details, and baseline/history entry points.
- Load creator-owned personal projects, remember the last opened project, and switch project context.
- If no project exists, show **Create your first project** using the existing personal-project flow.
- Define responsive behavior for desktop, tablet, mobile browser chrome, keyboard, and reduced motion.

**Acceptance:** the canvas fills the visible browser viewport without outer scrolling at supported
viewports; no-project, loading, error, one-project, and many-project states work; project options never
cross personal ownership or active workspace boundaries.

**Delivered evidence:** `/member/planner` is now a standalone guarded route outside `MemberLayout`,
with a `100dvw × 100dvh` Excalidraw host, compact project/progress/save/tool overlays, creator-owned
personal-project loading and switching, remembered last project, recoverable loading/empty states, and
an inline create-project drawer using the existing personal-project endpoint and shared validation.
Each project has an isolated temporary browser scene key pending Phase 18. Responsive behavior was
verified in the real component preview at 1280×720 and 390×844, including the mobile drawer and project
switching. Angular development build, lint/design lint, and Storybook build pass; five focused Jasmine
specs compile, but the local ChromeHeadless process crashed in its GPU sandbox before executing them.

## ✅ Phase 18 — Planner board domain and cloud persistence (2026-08-28)

**Outcome:** a project board survives refresh/device changes and cannot be silently overwritten.

- Add `PlannerBoard`, `PlannerSceneRevision`, and `PlannerNode` persistence/configurations/migration.
- Use one primary board per personal project and stable UUID node identities.
- Add authorized board/scene load and debounced save APIs with revision/ETag concurrency checks.
- Store current scene JSON without binary media; retain immutable checkpoint revisions.
- Add frontend repository/facade state for loading, saving, saved, offline, failed, and conflict.
- Keep IndexedDB only as a recovery cache and provide recovery/conflict UX.
- Add domain, handler, ownership, concurrency, API integration, and frontend autosave tests.

**Acceptance:** another device restores the board; stale tabs receive a conflict; user B cannot read or
write user A's board; network failure does not falsely report Saved; no scene contains base64 assets.

**Delivered evidence:** added the board, scene-revision, and node domain/persistence model, a migration
that backfills one primary board for every existing personal project, owner-authorized load/save/history
APIs, UTF-8 scene-size validation, immutable revisions, ETags, and database-safe optimistic concurrency.
The Angular Planner now uses cloud authority with debounced autosave, ordered IndexedDB recovery,
offline/failed/conflict states, explicit local-vs-server conflict resolution, and embedded-file blocking.
Disposable-PostgreSQL HTTP integration tests prove migration backfill, cross-user 403 isolation,
cross-device restore, stale saves, and simultaneous-write handling. All 12 backend and 230 frontend
tests pass; frontend build, lint/design lint, and Storybook build pass; EF reports no model drift.

## ✅ Phase 19 — Linked project, task, and subtask objects (2026-08-28)

**Outcome:** canvas objects operate on canonical TaskFlow work records and show live progress.

- Define Project/Task/Subtask Planner node contracts and stable Excalidraw-element mappings.
- Create/edit through Planner-aware commands that reuse current domain invariants and authorization.
- Use an inspector for business fields; keep layout/connector properties in Excalidraw scene data.
- Extend project fields with problem statement, budget amount/currency, and approximate duration weeks
  using a migration and backward-compatible DTO changes.
- Show derived total/completed task/subtask counts, status, dates, and completion percentage.
- Rehydrate current backend state so changes made elsewhere in TaskFlow appear on Planner nodes.

**Acceptance:** creating/editing a visual work item updates the canonical record exactly once; status
changes outside Planner are reflected; deleting/unlinking has explicit semantics; counts are backend
derived and cannot be forged by scene JSON.

**Delivered evidence:** `PlannerNode` now carries unique, server-owned Project/Task/Subtask links while
Excalidraw retains only element layout and a cached node id. Six owner-authorized workspace/node routes
atomically link the project, create tasks/subtasks, edit canonical fields, refresh backend-derived
status/counts/progress, and explicitly unlink a card or delete its underlying work item. Project records
now include problem statement, budget amount/currency, and approximate duration weeks through migration
`AddPlannerLinkedWorkItems`; existing clients remain backward compatible. The Angular workspace adds
linked-work creation, automatic missing-card rehydration, live labels, a compact inspector, and clear
unlink/delete controls. Disposable-PostgreSQL HTTP tests prove ownership, atomic creation, external
completion refresh, planning-field persistence, and removal semantics. All 15 backend and 231 frontend
tests pass; production build, lint/design lint, Storybook, detector, and EF model-drift checks pass.

## ✅ Phase 20 — Admin-managed template library (2026-08-28)

**Outcome:** admins publish safe, versioned Project/Task/Subtask/Note/Document building blocks.

- Add `PlannerTemplate` and immutable `PlannerTemplateVersion` records and migration.
- Support Draft → Published → Archived lifecycle, ordering, icon, header/title, color, dimensions,
  visible fields, and validated default values.
- Add AdminOnly management/publication APIs and admin UI; add a member read API and Excalidraw library
  integration.
- Limit the initial release to the five fixed object types; no executable templates or arbitrary DB
  schemas.
- Snapshot template version on node creation; published edits never mutate existing nodes.

**Acceptance:** only admins manage templates; members see only published active versions; archived
templates remain renderable for old nodes; invalid type/default combinations are rejected server-side.

**Delivered evidence:** `PlannerTemplate` and immutable `PlannerTemplateVersion` records, node version
snapshots, fixed per-type field/default validation, Draft/Published/Archived lifecycle, and migration
`AddPlannerTemplateLibrary` are complete. Six routes provide AdminOnly list/create/edit/publish/archive
and member published-active reads. Published edits append a version; existing cards retain their
original presentation after later edits or archive. Angular adds `/admin/planner-templates` and a live
Planner library using template defaults, dimensions, and colors. Note/Document definitions are visible
but intentionally await Phase 21 resource records. Backend tests pass 18/18; frontend specs pass 232/232;
build, lint/design lint, Storybook, detector, and EF model-drift checks pass.

## ✅ Phase 21 — Notes, documents, and secure media (2026-08-28)

**Outcome:** project plans can reference the material needed during execution without bloating scenes.

- Add `PlannerResource` and `PlannerAsset` metadata with project/board/node ownership.
- Use existing `IObjectStorage` for binary content (S3-compatible production, local development).
- Support notes, links, PDFs, images, audio, video, and generic documents through explicit limits.
- Add authorized upload, preview/download, rename/metadata update, unlink, and delete flows.
- Add checksum, safe filename/content-disposition, size/type validation, and scanning-status hook.
- Keep all binary/base64 content out of PostgreSQL scene JSON.

**Acceptance:** every resource is ownership-checked; unsupported/oversized files fail clearly; scene
saves remain small; deleting a board/project follows an explicit asset-retention/deletion policy.

**Delivered evidence:** `PlannerResource` and `PlannerAsset` records, exact resource-node targets, and
migration `AddPlannerResourcesAndAssets` now separate notes/link/file metadata from binary objects.
Eight owner-authorized routes cover list, note/link creation, multipart upload, preview/download,
metadata/filename update, relink, and permanent deletion. Uploads use private `IObjectStorage`, a 25 MB
limit, extension/content-type allowlist, safe names, SHA-256, scan status, and a replaceable scanner
hook. Angular adds resource cards, forms, preview/download, inspector editing, and an unlinked-resource
library. Unlink retains metadata/object; explicit delete removes both; project soft-delete retains them
for recovery. HTTP/PostgreSQL tests prove isolation, validation, relinking, deletion, and scene exclusion.
Backend tests pass 21/21 and frontend specs pass 234/234; build, lint/design lint, and EF checks pass.

## ✅ Phase 22 — Primary requirements, changes, and comparison (delivered above)

**Outcome:** the initial plan becomes an immutable primary baseline and later scope changes are fully
traceable.

- Add `RequirementBaseline`, `RequirementSnapshot`, and `RequirementChange` plus migration.
- Implement atomic **Finalize primary requirements** for project/task/subtask requirement fields and
  ordering.
- After finalization, Planner-aware mutations record New, Changed, or Removed/Deprecated server-side,
  including actor, timestamp, old/new values, and optional reason.
- Keep execution progress (status, completion, time logs) separate from requirement changes.
- Add baseline list/detail, working change set, field-level comparison, and filters.
- Preserve Baseline 1 forever; design the model so later baselines can be finalized without rewrite.

**Acceptance:** baseline creation is transactional and immutable; ordinary/direct clients cannot bypass
change history; original/current values are comparable; progress alone does not mark a requirement
Changed; ownership checks cover every history read and write.

## ✅ Phase 23 — Hardening, scale, and production rollout (delivered above)

**Outcome:** Planner is observable, secure, performant on realistic boards, and safely deployable.

- Load/performance-test large scenes, node hydration, project switching, revisions, and uploads.
- Add indexes, payload/file limits, revision retention/archival policy, structured metrics, tracing,
  audit logs, and operational alerts.
- Complete security review for IDOR, malicious scene JSON, upload attacks, XSS, and signed access.
- Add API/domain/integration tests and critical browser flows: empty project, autosave/recovery,
  conflict, template versioning, upload, baseline finalization, diff, and cross-user denial.
- Roll out behind a feature flag, migrate existing per-user browser scenes through an explicit import
  choice, monitor, then remove the prototype-only path after the rollback window.

**Acceptance:** agreed performance/error targets pass in staging; rollback and migration are tested;
no silent data loss or cross-user access occurs; documentation and completion ledger match production.

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
