# TaskFlow Planner — Product Requirements and End-to-End Delivery Plan

> **Status:** implementation complete; Phases 17–23 delivered and production rollout verified.
> **Created:** 2026-08-27.
> **Canonical context:** read this document before planning or implementing any Planner work. Keep
> [PHASES.md](PHASES.md) and [ProjectCompletion.md](ProjectCompletion.md) synchronized as phases land.

## 1. Product intent

Planner is a project-scoped visual planning workspace for Individual accounts. It embeds Excalidraw
for free-form spatial editing, while TaskFlow remains authoritative for projects, tasks, subtasks,
requirements, documents, progress, ownership, and history.

Planner must let a user:

- define a project and the problem it solves;
- create and arrange tasks and subtasks in the intended execution order;
- see total/completed task and subtask counts and completion dates;
- connect work items into a map or flow;
- attach the notes, links, PDFs, images, audio, video, and other documents needed during execution;
- finalize the initial plan as an immutable **primary requirement baseline**;
- identify requirements added, changed, or removed after that baseline without destroying the
  original; and
- inspect earlier requirement versions and compare them with the current plan.

This is not a second task database and not merely a saved drawing. Excalidraw is the interaction and
layout engine; the TaskFlow domain and API are the source of truth.

## 2. Scope and privacy boundary

The first production scope is the signed-in Individual account's creator-only personal projects.
Existing rules remain structural:

- a personal project has `OrganizationId = null` and belongs only to `CreatedByUserId` from the JWT;
- joining or owning an organization must never expose, import, assign, or report personal Planner
  data;
- every board, scene, node, asset, baseline, snapshot, and change is authorized on the server through
  its owning project and creator;
- organization Planner support is a later, explicit extension that must use organization membership
  and permissions. It must not be introduced by weakening personal ownership rules.

## 3. Workspace experience

### 3.1 Full-viewport canvas

`/member/planner` must use a dedicated authenticated immersive layout rather than the normal member
content container. The Planner occupies `100dvw × 100dvh`, uses `100dvh` for mobile browser chrome,
has no document scrolling, outer page padding, `1200px` maximum width, rounded canvas card, or large
page heading. Excalidraw retains ownership of its native drawing controls.

TaskFlow adds compact overlay controls rather than shrinking the canvas:

- return to TaskFlow;
- searchable project selector and Create project action;
- live project progress;
- save/sync state;
- template library;
- project details/inspector;
- Finalize primary requirements;
- requirement history and comparison.

### 3.2 Project selection and empty state

- One primary Planner board per project in the first release.
- Load the most recently opened project when possible.
- The project selector lists only projects visible in the active personal workspace.
- When no project exists, show a centered **Create your first project** action using the existing
  personal-project creation flow.
- Changing projects loads that project's scene, nodes, resources, baseline, and progress.
- Multiple boards per project and organization boards are deliberate future extensions, not Phase 17
  scope.

### 3.3 Inspector and live status

Selecting a TaskFlow object opens a compact inspector for its business fields. Counts and progress are
read-only derived data from the backend. Completing or editing a task elsewhere in TaskFlow must be
reflected when its Planner node is loaded or refreshed.

## 4. Object and template requirements

### 4.1 Supported object types

The initial admin-managed library contains fixed, validated types:

1. **Project template**
   - title;
   - description;
   - total and completed task/subtask counts (derived, not entered);
   - optional organization association for the later organization-enabled scope;
   - problem statement;
   - budget amount and currency;
   - approximate completion duration in weeks.
2. **Task template** — TaskFlow task fields, progress, requirement state, and visual defaults.
3. **Subtask template** — TaskFlow subtask fields, completion state, and visual defaults.
4. **Note template** — text/markdown planning content linked to the board or a work item.
5. **Document template** — a link to a managed file/resource and its metadata.

Do not implement arbitrary admin-authored database schemas in the first version. Each supported type
has a stable server contract; admins configure presentation and allowed defaults.

### 4.2 Admin configuration

An admin can configure a template's name, object type, icon, header/title, colors, default dimensions,
visible fields, default values, sort order, and active state. Templates follow Draft → Published →
Archived lifecycle.

Every published edit creates an immutable `PlannerTemplateVersion`. Existing nodes retain the version
with which they were created, so an admin change never silently mutates an existing project plan.

## 5. Source-of-truth and persistence rules

### 5.1 Business data versus scene data

Projects, tasks, subtasks, requirements, assets, ownership, status, dates, and progress live in
TaskFlow records. Excalidraw owns position, dimensions, connectors, grouping, color, viewport, zoom,
and other canvas presentation.

The stable relationship is:

```text
Excalidraw element id → PlannerNode → TaskFlow entity/resource id
```

`customData` in an Excalidraw element may cache the link for rendering, but it is not an authorization
boundary or canonical business record. Counts and completion percentages are calculated by the API.

### 5.2 Recommended domain records

| Record | Responsibility |
|---|---|
| `PlannerBoard` | Owner/project association, current scene revision, last-opened metadata |
| `PlannerSceneRevision` | Versioned Excalidraw scene JSON and revision metadata |
| `PlannerNode` | Stable UUID and link from an element to Project/Task/Subtask/Note/Document |
| `PlannerTemplate` | Admin-owned template identity, type, lifecycle, and ordering |
| `PlannerTemplateVersion` | Immutable published schema/presentation/default snapshot |
| `PlannerResource` | Note, link, or document metadata and board/project relationship |
| `PlannerAsset` | Stored-object metadata for uploaded binary content |
| `RequirementBaseline` | Immutable finalized requirement checkpoint for a project |
| `RequirementSnapshot` | Project/task/subtask requirement state captured by a baseline |
| `RequirementChange` | Added/Changed/Removed delta after a baseline, with audit metadata |

Planner-owned records should use UUIDs so the browser can generate stable identities before a save.
Existing integer Project/Task/Subtask ids remain unchanged.

### 5.3 Scene autosave and recovery

- Save scene changes to the API with a short debounce; do not send a request for every pointer event.
- Store the current scene as PostgreSQL `jsonb` and retain immutable revisions at meaningful
  checkpoints. Introduce compression/object-storage archival only after measurement proves it needed.
- Use a revision number/ETag or concurrency token. A stale browser tab must receive a conflict instead
  of silently replacing newer work.
- Display `Saving`, `Saved`, `Offline`, `Conflict`, and `Failed` states.
- Keep a temporary IndexedDB recovery copy, but the server remains canonical.
- Do not put uploaded files or base64 media inside scene JSON.

### 5.4 Files and rich resources

PostgreSQL stores ownership, relationships, filename, content type, size, checksum, storage key,
uploader, timestamps, and processing state. The existing `IObjectStorage` abstraction stores binary
content (S3-compatible provider in production; local filesystem in development). Downloads/previews
must be authorized and use short-lived access where applicable.

Apply file-size/type limits, safe filenames, content-disposition controls, and a malware-scanning hook
before general availability. A canvas document node references a `PlannerAsset`; it never embeds the
binary file.

## 6. Primary requirement baseline and change history

### 6.1 Before finalization

Projects, tasks, and subtasks are Draft requirements and can be revised freely. The UI action is named
**Finalize primary requirements** and must explain that the resulting baseline cannot be overwritten.

### 6.2 Finalization

Finalization runs as one server-side transaction:

- create `RequirementBaseline` (initially Baseline 1);
- snapshot the relevant project, tasks, subtasks, ordering, and requirement-bearing fields;
- record who finalized it and when;
- make those snapshots immutable.

### 6.3 Changes after finalization

- A requirement created after the active baseline is labelled **New**.
- A baseline requirement whose scope-bearing fields change is labelled **Changed**.
- A removed requirement is labelled **Removed** or **Deprecated**; history is not hard-deleted.
- The original snapshot remains viewable beside the current value.
- Every change records actor, time, old/new values, and an optional reason.
- Execution-only changes (Todo → In progress → Completed, completion timestamps, time logs) are
  progress, not requirement changes.

The server must enforce this through Planner-aware create/update/remove commands. This cannot be a UI
badge added after ordinary mutation, because other clients or direct API calls could bypass history.

The comparison experience must support baseline/current values, field-level differences, actor/time,
reason, and New/Changed/Removed filters. A later release may finalize the working change set as
Baseline 2 while preserving every earlier baseline.

## 7. API capability families

Exact route names are decided during implementation, but the API must expose coherent capability
families rather than one oversized scene endpoint:

- personal Planner project list and project creation handoff;
- board load/create and current scene load/save with concurrency token;
- scene revision/history and recovery;
- node create/link/update/remove commands that preserve domain invariants;
- template list for members and template administration/version publication for admins;
- resource metadata, file upload/download/delete, and authorization;
- primary requirement finalization;
- baseline list/detail, current change set, and baseline comparison;
- project progress summary for canvas overlays.

All controllers require authentication. Personal-resource queries and commands resolve ownership from
the JWT and owning project; organization ids or creator ids are never trusted from a request body.

## 8. Delivery phases

The detailed executable roadmap is maintained in [PHASES.md](PHASES.md):

- **Phase 17 (complete 2026-08-28):** immersive full-viewport shell and project selection;
- **Phase 18 (complete 2026-08-28):** board domain, cloud persistence, autosave, recovery, and concurrency;
- **Phase 19 (complete 2026-08-28):** linked Project/Task/Subtask nodes and live progress;
- **Phase 20 (complete 2026-08-28):** admin template library and immutable template versions;
- **Phase 21 (complete 2026-08-28):** notes, documents, and secure media storage;
- **Phase 22 (complete 2026-08-28):** primary baseline, change tracking, and comparison;
- **Phase 23:** hardening, performance, observability, and release rollout.

Each phase must ship backend authorization and tests with its UI integration. Do not postpone security,
ownership, or conflict handling until the final phase.

## 9. Non-goals for the first end-to-end release

- real-time multi-user cursor collaboration;
- arbitrary template-defined database schemas or executable code;
- multiple boards per project;
- organization Planner permissions and shared editing;
- offline-first synchronization across devices;
- AI-generated project plans;
- storing binary media in PostgreSQL or Excalidraw JSON.

The model should leave these upgrade paths open without building them prematurely.

## 10. Cross-cutting acceptance criteria

- Planner is genuinely full viewport at desktop and mobile browser sizes with no outer page scroll.
- A user with no project can create one; a user with projects can switch without data crossing boards.
- Refreshing or signing in on another device restores the authoritative project scene from the server.
- Two stale tabs cannot silently overwrite each other.
- Canvas nodes remain linked to canonical TaskFlow entities and show current progress.
- Cross-user reads and writes of every personal Planner record return 403/404 according to the
  existing security convention.
- Published template edits do not mutate nodes created from older versions.
- Finalizing the primary requirement is atomic and immutable.
- New/Changed/Removed flags are produced by server-side history, not inferred only in the browser.
- Requirement changes and execution progress are visibly and semantically distinct.
- Uploaded resources are authorized, size/type constrained, and absent from scene JSON.
- Automated domain, handler, authorization, persistence/concurrency, API integration, Angular unit,
  and critical browser-flow tests cover the feature before general release.

## 11. Documentation maintenance

Whenever a Planner phase changes:

1. update its status and evidence in [PHASES.md](PHASES.md);
2. update API/UI parity and the changelog in [ProjectCompletion.md](ProjectCompletion.md);
3. record implementation decisions that modify the architecture in
   [ARCHITECTURE.md](ARCHITECTURE.md);
4. add a concise handoff entry to [SESSIONS.md](SESSIONS.md); and
5. update this file when product scope, invariants, or acceptance criteria change.
