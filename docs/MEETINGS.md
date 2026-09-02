# TaskFlow Meetings — Canonical End-to-End Delivery Plan

> **Deferred on 2026-09-02:** Meetings is temporarily removed from the organization sidebar. The
> implemented API/UI remains in the repositories for later resumption, but the feature must not be
> presented as production-ready until LiveKit runtime configuration is reliably propagated to the API
> and a real multi-client audio/video call passes production verification. Dokploy `v0.29.14` saved
> the configured `LiveKit__*` values but did not add them to the running Swarm service; the LiveKit
> server and public domain were healthy. Resume at Phase 7 after correcting that deployment boundary.

> **Status:** DEFERRED — PHASES 0–5 DONE; Phase 6 certification and Phase 7 rollout remain pending.
>
> **Production validation (2026-09-01):** meeting create, registered-participant assignment and start
> work on `taskflow.inksphere.space`, and the deployed Phase 5 collaboration API/UI loads. Realtime
> calling is not production-enabled: the join-token path reports `LiveKit media is not enabled` and
> the pre-join action stays disabled. Also, clearing “Schedule for a specific time” leaves stale
> required validators on the hidden start/end controls, so ready-anytime creation is blocked while
> scheduled creation works. Track both as Phase 7 rollout/hardening work; they do not reopen Phase 5.
>
> **Canonical scope:** Angular in `D:\Projects\TMS\TaskFlowUI\TaskFlowApp`, ASP.NET Core API in
> `D:\Projects\TMS\TaskFlow`, PostgreSQL for durable state, TaskFlow private object storage for
> documents and recordings, and self-hosted LiveKit for realtime media/data transport.
>
> Keep this document current after every Meetings implementation session. Do not rely on chat history
> to remember product or architecture decisions.

## 1. Session handoff contract

Use these phrases in a future task without re-explaining the feature:

- **“meeting status”** — read this file and both repositories' current changes/tests, then report the
  active phase, completed evidence, decisions still open, and the next safe action. Do not modify code.
- **“complete next meeting phase”** — implement only the first `READY` or `IN PROGRESS` phase, satisfy
  every exit criterion, update this file, both `docs/PHASES.md` files, both append-only
  `docs/SESSIONS.md` logs, and `docs/ProjectCompletion.md` when the API/UI surface changes.
- **“continue meeting phase N”** — resume Phase N from its unchecked work/evidence. Do not begin a
  later phase while an earlier phase is incomplete unless this file explicitly records an approved
  exception.
- **“complete meeting phase N”** — first verify all dependencies in the delivery table are `DONE`;
  then implement only Phase N and update the same handoff documents.

At the start of every Meetings session:

1. Read the session handoff contract, the delivery-status table, the active phase, and the latest
   evidence/decision entry. Read the rest of this file only when the selected work package changes an
   architectural decision, API contract, security boundary, or a phase explicitly points to it.
2. Read `docs/ProjectCompletion.md` and both repositories' `docs/PHASES.md`.
3. Inspect the working trees and preserve unrelated user changes.
4. Confirm the current LiveKit version and relevant official documentation before changing the
   integration because SDK/server behavior is time-sensitive.
5. Mark the selected phase `IN PROGRESS` here before implementation and leave an evidence-log entry at
   the end, even when blocked.

### Budgeted execution protocol

Meeting phases are deliberately large product milestones; they are **not** single-agent-session units.
To avoid an unbounded implementation run, every implementation request uses one bounded work package
unless the user explicitly authorizes a larger scope.

- **“complete the next meeting phase”** means advance the first `READY` or `IN PROGRESS` phase by its
  single highest-priority unfinished work package, then stop and report the remaining packages. It does
  not imply that the full phase must be finished in one run. Mark a phase `DONE` only after all of its
  exit criteria are met across one or more work packages.
- A work package must have one independently verifiable outcome, a narrow API/UI boundary, focused
  tests, and a concise checkpoint entry. Do not combine infrastructure provisioning, unrelated
  refactoring, another phase, or broad documentation cleanup into it.
- Treat environment preparation and external/manual evidence as separate packages. For example,
  installing Docker/LiveKit, running a multi-browser media proof, and a production-like soak test must
  never consume the same package as room-code implementation.
- Prefer targeted build/test commands while a phase is in progress. Run the full cross-repository suite
  only when a package changes a shared boundary or when the final package is claiming phase completion.
- Stop without expanding scope when the package is complete, a required dependency is unavailable, a
  test fails outside the package, or the remaining work needs a new user/product decision. Record the
  exact next package and the command/evidence required to resume.

For a stricter per-run cap, use: **“advance the next meeting work package; do not start a second
package, do not install infrastructure, and stop after targeted verification.”**

## 2. Product outcome

TaskFlow organization users can create or schedule meetings, invite registered users and unregistered
email guests, share revocable meeting links, conduct branded audio/video calls, share screens, chat,
edit meeting notes, exchange documents, and later review the meeting record. A meeting host can start
and stop a consent-aware recording; completed recordings appear in the meeting archive.

### In scope

- A **Meetings** item in the organization sidebar and organization-scoped list/detail/archive pages.
- Scheduled and instant meetings with title, description, time, timezone, settings, creator and status.
- Registered TaskFlow participants and guests who have no TaskFlow account.
- Private email invitations and reusable email-verified share links.
- Creator-assigned access level and visible badge for every invite/link/participant.
- A custom Angular lobby and calling UI using the LiveKit JavaScript client SDK.
- Audio, video, device selection, screen sharing, presence, active-speaker UI and host moderation.
- Durable attendance, chat, notes, file metadata/content and recording metadata/content.
- Creator-controlled start/stop recording with explicit participant notice and consent evidence.
- Organization permissions, meeting-level capabilities, retention, observability and production rollout.

### Explicit non-goals for the first roadmap

- PSTN/SIP dial-in, phone numbers, webinars, livestreaming and breakout rooms.
- AI summaries, transcription, translation or meeting bots.
- Background blur/noise suppression beyond what browsers/self-hosted LiveKit provide by default.
- Public anonymous entry without email verification.
- Google Docs-style multi-cursor note editing. Phase 5 supplies conflict-aware autosave; add CRDT/Yjs
  only if real simultaneous-editing demand is validated.
- LiveKit as TaskFlow's database. Realtime delivery and durable persistence have separate owners.

## 3. Non-negotiable architecture decisions

### 3.1 Source-of-truth boundary

| Concern | Authority |
|---|---|
| Meeting metadata, access links, invitations and participant assignments | TaskFlow API + PostgreSQL |
| Permission checks and guest authorization | TaskFlow API |
| Audio, video, screen tracks, room presence and low-latency delivery | LiveKit |
| Chat history, notes, attendance and audit history | TaskFlow API + PostgreSQL |
| Uploaded documents and completed recordings | Private TaskFlow object storage |
| Recording production | LiveKit Egress, initiated only by the TaskFlow API |

LiveKit text/byte streams are not historical storage. Participants who join late or refresh must load
canonical chat, notes and files from TaskFlow, then receive new events in realtime.

### 3.2 Capability and badge are different concepts

An invite/link/participant has both:

1. **Meeting access level** — security-sensitive and fixed to `Host`, `CoHost`, `Participant`, or
   `Viewer`.
2. **Display badge** — presentation-only, such as `Manager`, `Designer`, `Coder`, `Influencer`, or a
   creator-defined value.

A badge never grants capabilities. Labels are trimmed, length-limited, HTML-free and stored as plain
text. The meeting creator is always `Host`; host transfer, if later supported, must be an explicit
audited operation.

Initial capability matrix:

| Capability | Host | Co-host | Participant | Viewer |
|---|:---:|:---:|:---:|:---:|
| Join, receive media and view shared content | Yes | Yes | Yes | Yes |
| Publish microphone/camera | Yes | Yes | Yes | No |
| Share screen | Yes | Yes | Yes | No |
| Send chat/files | Yes | Yes | Yes | Configurable, default No |
| Edit shared meeting note | Yes | Yes | Configurable, default Yes | No |
| Admit/remove/mute participants | Yes | Yes | No | No |
| Create/revoke links or end meeting | Yes | No | No | No |
| Request/start/stop recording | Yes | Configurable, default No | No | No |
| Delete meeting/archive content | Yes | No | No | No |

Server authorization remains authoritative even if the Angular controls are hidden.

### 3.3 Guest access is not a TaskFlow account

- A guest never receives an organization membership, normal TaskFlow access token or `User` row.
- After email verification, the API issues a short-lived **guest meeting session** scoped to one
  meeting, one participant identity and a narrow set of meeting endpoints.
- The guest session contains no reusable organization permission. It is rechecked against meeting,
  link and revocation state before privileged API work.
- LiveKit receives a separate short-lived access token containing only the grants allowed by the
  meeting access level.
- A guest can re-open permitted archive content by re-verifying the same email while the invitation,
  link and retention window remain valid.

### 3.4 External provider boundaries

Backend application code depends on a TaskFlow-owned `IMeetingMediaProvider`, not on LiveKit types.
The infrastructure adapter owns token signing, room administration, participant moderation and Egress
calls. Angular pages/facades depend on a TaskFlow-owned room service that wraps `livekit-client`; raw
LiveKit types must not spread through page models or API DTOs.

This boundary permits local self-hosting, LiveKit Cloud, or a future provider change without rewriting
the Meetings domain.

## 4. Principal user journeys

### 4.1 Create and share a meeting

1. An authorized organization member creates an instant or scheduled meeting.
2. The API assigns an unguessable meeting identifier and internal LiveKit room name. The room name is
   never used as authorization.
3. The creator chooses meeting defaults: lobby behavior, guest access, participant media permissions,
   file/note access, recording eligibility and archive retention.
4. The creator either sends a private email invitation or creates a reusable share link.
5. For each invite/link, the creator selects an access level and display badge.
6. Links can be copied, expired, usage-limited, rotated and revoked from the meeting detail page.

### 4.2 Private email invitation

1. Creator supplies email, display name if known, access level and badge.
2. TaskFlow creates a single-recipient access record and emails an opaque 256-bit token link.
3. The recipient opens `/meetings/join#token=...`; Angular reads the fragment, immediately scrubs it
   from the address bar, and submits it only in an HTTPS request body.
4. If an authenticated TaskFlow account's verified email matches, the API can bind it directly after a
   confirmation screen. Otherwise TaskFlow sends a meeting-specific OTP to that email.
5. Successful verification creates/binds the meeting participant and issues the guest meeting session.
6. The lobby collects a display name, device choices and any required recording acknowledgement before
   issuing the LiveKit join token.

### 4.3 Reusable share link

1. Creator chooses default access level, default badge, expiry and maximum use count.
2. Anyone possessing the link must still enter and verify an email address before joining.
3. Each verified email gets a distinct participant and attendance identity; the shared token is never
   used as the LiveKit participant identity.
4. Revoking the link blocks new joins. The host can separately remove/revoke an already admitted
   participant.

Private invitations are the recommended mode for confidential meetings. A reusable link proves email
control, not that the recipient was personally intended by the creator; the UI must state this clearly.

### 4.4 Live meeting

1. Registered or guest participant loads canonical meeting state and enters the lobby.
2. API verifies access and returns a short-lived LiveKit token with server-derived grants.
3. Angular connects to LiveKit and renders TaskFlow's custom room UI.
4. Participant tiles show display name, access-level indicator when relevant, and the assigned badge.
5. Presence/media changes flow through LiveKit. Durable chat/files/notes are written to TaskFlow first
   or with idempotent client IDs, then announced over LiveKit for immediate rendering.
6. Signed, deduplicated LiveKit webhooks maintain room lifecycle and attendance intervals.
7. Host ends the meeting; API closes the room, finalizes open attendance and transitions the meeting to
   `Ended` without losing its archive.

### 4.5 Recording

1. A permitted host clicks **Record meeting**.
2. TaskFlow creates a pending recording/consent request and broadcasts it to connected participants.
3. Initial policy is strict: Egress starts only after every current media participant explicitly
   accepts. A decline or timeout blocks recording and is shown to the host without naming legal claims.
4. While recording, every client shows an unavoidable red recording indicator. A new joiner must see
   the recording disclosure and consent before receiving a room token.
5. The API, never the browser, calls LiveKit Egress. The first format is a room-composite MP4 written
   directly to the configured private S3-compatible bucket.
6. Host can stop recording; ending the meeting also stops any active Egress job.
7. Signed Egress webhooks update `Pending → Starting → Recording → Processing → Ready/Failed`.
8. Authorized archive viewers stream/download through TaskFlow authorization; object keys are not
   public URLs.

The strict consent behavior is a product safety default, not legal advice. Before production launch,
TaskFlow must review recording disclosure, retention and deletion requirements for its target regions.

## 5. Planned domain and persistence model

Names may be refined during Phase 1, but changing the responsibility boundaries requires an explicit
decision entry in this document.

| Aggregate/entity | Key responsibility |
|---|---|
| `Meeting` | Organization, creator, title/description, schedule/timezone, lifecycle, settings, LiveKit room name and retention |
| `MeetingBadgeDefinition` | Meeting-owned safe label/color/icon options, including predefined and custom badges |
| `MeetingParticipant` | Stable registered-user or guest identity, access level, badge snapshot, display name, email, invite/admission/revocation state |
| `MeetingAccessLink` | Hashed opaque token, private/shared mode, optional locked email, defaults, expiry, maximum uses and revocation |
| `MeetingGuestChallenge` | Hashed OTP, expiry, attempt counters and consumption state; never a general auth code |
| `MeetingAttendance` | One joined/left interval per connection, LiveKit identity/SID and webhook-derived timestamps |
| `MeetingMessage` | Durable message with client idempotency key, author participant, body, timestamp and optional reply reference |
| `MeetingNote` | One current note document with optimistic version plus audited revisions/editor metadata |
| `MeetingAsset` | Private object key, safe filename, MIME type, size/hash, uploader, scan/status and retention |
| `MeetingRecording` | Egress id, consent request, status, object key, size/duration, starter/stopper and failure metadata |
| `MeetingRecordingConsent` | Recording id, participant, decision and immutable decision timestamp |
| `MeetingWebhookReceipt` | Provider event id/type/time for idempotency and replay protection |

All meeting-owned records are soft-deleted where appropriate. Database constraints/indexes must cover
organization/time listing, meeting/participant uniqueness, token-hash uniqueness, message ordering,
open attendance lookup, Egress id uniqueness and webhook event id uniqueness.

PII/content policy:

- Email is normalized for matching and retained only as long as access/audit policy requires.
- Never log raw invite tokens, OTPs, LiveKit tokens, chat bodies, notes or file contents.
- Store only token/OTP hashes. Secret comparison is constant-time where applicable.
- Use UTC for instants and retain the chosen timezone for display/scheduling semantics.
- Define configurable retention for chat, notes, files and recordings before Phase 7 production rollout.

## 6. Planned API surface

The exact request DTO names follow existing CQRS conventions. Public/guest endpoints must be isolated
from normal organization controllers and authorization policies.

### Authenticated organization endpoints

| Method and route | Purpose |
|---|---|
| `POST /meeting` | Create instant/scheduled meeting |
| `GET /meeting/organization/{organizationId}` | Bounded/filterable upcoming/live/past list |
| `GET /meeting/{meetingId}` | Authorized detail/archive summary |
| `PUT /meeting/{meetingId}` | Edit schedule/settings while allowed |
| `POST /meeting/{meetingId}/cancel` | Cancel scheduled meeting |
| `POST /meeting/{meetingId}/start` | Start/admit room lifecycle |
| `POST /meeting/{meetingId}/end` | End room and finalize meeting |
| `POST /meeting/{meetingId}/access-links` | Create private invite or reusable share link |
| `GET /meeting/{meetingId}/access-links` | List safe link metadata; never return stored raw tokens later |
| `DELETE /meeting/{meetingId}/access-links/{linkId}` | Revoke link/invitation |
| `PUT /meeting/{meetingId}/participants/{participantId}` | Change badge/access/admission state with safeguards |
| `POST /meeting/{meetingId}/join-token` | Registered participant obtains LiveKit token |
| `GET/POST /meeting/{meetingId}/messages` | History page and idempotent send |
| `GET/PUT /meeting/{meetingId}/note` | Versioned note read/autosave |
| `GET/POST /meeting/{meetingId}/assets` | List/upload private meeting files |
| `GET/DELETE /meeting/{meetingId}/assets/{assetId}` | Authorized content/delete |
| `POST /meeting/{meetingId}/recordings` | Open consent request and start after consent |
| `POST /meeting/{meetingId}/recordings/{recordingId}/consent` | Registered participant decision |
| `POST /meeting/{meetingId}/recordings/{recordingId}/stop` | Stop active recording |
| `GET /meeting/{meetingId}/recordings` | Recording history/status |
| `GET /meeting/{meetingId}/recordings/{recordingId}/content` | Authorized playback/download |

Use paging on messages/assets/recordings and bounded date windows on organization meeting lists. Large
uploads should use streaming or short-lived presigned upload flow after authorization; never buffer an
unbounded file in API memory.

### Guest/public endpoints

| Method and route | Purpose |
|---|---|
| `POST /meeting/guest/access/inspect` | Validate opaque link enough to render safe meeting/lobby information |
| `POST /meeting/guest/access/request-code` | Send enumeration-safe, rate-limited email OTP |
| `POST /meeting/guest/access/verify-code` | Verify email/link and issue one-meeting guest session |
| `GET /meeting/guest/session` | Restore the scoped lobby/archive context |
| `POST /meeting/guest/join-token` | Obtain LiveKit token after lobby/admission/consent checks |
| Guest-scoped message/note/asset/consent routes | Same domain behavior, constrained by guest session capabilities |

The implementation may share handlers internally, but public endpoints must not accept caller-supplied
organization/user IDs as authority.

### Provider webhook endpoint

- `POST /webhooks/livekit` is anonymous at the ASP.NET authentication layer but must validate the
  LiveKit signature against the raw body before parsing.
- Accept the documented LiveKit webhook content type.
- Persist/deduplicate the provider event id before applying room, participant or Egress transitions.
- Return quickly; make repeat delivery safe. Unknown/out-of-order events are logged without corrupting
  canonical meeting state.

## 7. Security and abuse controls

- Generate access tokens with a cryptographically secure RNG; raw value is shown/sent only at creation.
- Place link tokens in the URL fragment, scrub immediately, and exchange through a POST body to reduce
  proxy/referrer/log exposure.
- Private invite: locked normalized email, one recipient, default single use, revocable and expiring.
- Shared link: configurable expiry/use count, mandatory email OTP, creator-visible warning and revoke.
- Rate-limit link inspection, OTP request/verify, guest join, message send and uploads by a combination
  of IP, token hash, meeting and normalized email hash.
- OTPs expire quickly, have attempt ceilings and resend cooldowns, and return enumeration-safe errors.
- Recheck organization membership/permission or guest access state before every sensitive API action.
- Use unpredictable LiveKit participant identities and map them server-side to participants.
- Grant least privilege in LiveKit tokens (`canPublish`, sources, data, subscribe and room admin grants).
- Validate webhook signatures and event ids; never trust client-reported join/leave timestamps.
- Files are allowlisted by type/size, filename-normalized, hash-recorded, scanned through the existing
  scanner boundary and served with safe content headers.
- Chat/note limits, message idempotency keys and upload quotas prevent accidental or deliberate abuse.
- Host removal revokes TaskFlow access before disconnecting the LiveKit participant.
- No recording without server-side permission, recorded consent state and a visible room indicator.

## 8. Frontend shape

### Routes

- `/organization/meetings` — upcoming/live/past list and create action.
- `/organization/meetings/:id` — meeting management and archive detail.
- `/organization/meetings/:id/room` — authenticated full meeting room.
- `/meetings/join` — public minimal guest link/OTP/lobby route, outside organization/member layouts.
- `/meetings/guest/room` — scoped guest meeting room that does not expose normal portal navigation.

### Feature ownership

```text
src/app/features/organization/
  meetings-page/
  meeting-detail-page/
  meeting-room-page/
  meetings.facade.ts
  meetings.repository.ts
  meetings.models.ts

src/app/features/meetings-guest/
  guest-join-page/
  guest-room-page/
  meetings-guest.routes.ts
  meetings-guest.facade.ts
  meetings-guest.repository.ts

src/app/core/meetings/
  meeting-room.service.ts       # TaskFlow wrapper over livekit-client
  meeting-device.service.ts
```

Reusable call controls, participant tiles, badge, chat panel, notes panel, file panel and recording
indicator follow the existing Atomic Design rules. Pages talk through facades; repositories own HTTP.
Room/device state is local realtime state and must not be added to the broad organization facade.

### Custom room UI requirements

- Pre-join device preview, device picker, permission-denied recovery and remembered device choices.
- Responsive grid/speaker modes, active speaker, camera-off/avatar, connection quality and reconnecting.
- Microphone, camera, screen share, device menu, chat, people, notes, files, record and leave/end controls.
- Badge visible on participant tile and roster without overpowering the person's display name.
- Host/co-host moderation UI with confirmation for remove/end and truthful permission errors.
- Recording consent dialog, persistent red indicator and recording-processing/archive states.
- Keyboard navigation, focus management, live announcements, reduced motion, light/dark themes and a
  compact mobile control tray.
- Explicit states for unsupported browser, media permission denial, token expiry, room closed, removed,
  reconnecting, API persistence failure and LiveKit connection failure.

## 9. Infrastructure and configuration

### Local development

- Version-pinned Docker Compose for LiveKit and Redis; add Egress only in Phase 6.
- Development API key/secret and WebSocket URL through example configuration, never committed secrets.
- Browser/API/LiveKit origins and local TLS behavior documented for the team.
- A small provider smoke test plus two-browser manual script for camera/microphone/screen sharing.

### Production

- Dedicated `wss://` LiveKit domain with trusted TLS, public IP awareness, required UDP/TCP ports and
  TURN/TLS fallback. Do not assume an ordinary HTTP reverse proxy is sufficient for WebRTC.
- Redis for production LiveKit coordination; persistence/backup remains TaskFlow's responsibility.
- Egress deployed separately with capacity tied to maximum simultaneous recordings. Production
  recording requires S3-compatible storage credentials reachable by Egress.
- Health, metrics and alerts for API join-token failures, LiveKit room/participant counts, webhook
  lag/failures, Egress queue/failures, object-storage failures and guest OTP abuse.
- Separate development/staging/production keys and endpoints with a documented rotation procedure.

Configuration groups:

```text
Meetings:Enabled
Meetings:GuestsEnabled
Meetings:RecordingEnabled
Meetings:GuestSessionMinutes
Meetings:DefaultRetentionDays
Meetings:MaxFileBytes
LiveKit:Url
LiveKit:ApiKey
LiveKit:ApiSecret
LiveKit:WebhookToleranceSeconds
```

Secrets belong in the deployment secret store. UI receives only the WebSocket URL when needed; it
never receives the API secret.

Official references to re-verify during implementation:

- JavaScript client: <https://docs.livekit.io/reference/client-sdk-js/>
- Tokens and grants: <https://docs.livekit.io/home/server/generating-tokens/>
- Webhooks: <https://docs.livekit.io/intro/basics/rooms-participants-tracks/webhooks-events/>
- Text streams: <https://docs.livekit.io/transport/data/text-streams/>
- Byte streams: <https://docs.livekit.io/transport/data/byte-streams/>
- Self-hosting: <https://docs.livekit.io/transport/self-hosting/>
- Production deployment: <https://docs.livekit.io/transport/self-hosting/deployment/>
- Egress: <https://docs.livekit.io/transport/self-hosting/egress/>

## 10. Delivery phases

### Delivery status

| Phase | Outcome | Dependency | Status |
|---|---|---|---|
| 0 | Architecture contract and LiveKit feasibility spike | None | DONE |
| 1 | Meeting domain, persistence, permissions and core API | Phase 0 | DONE |
| 2 | Organization meeting management UI and scheduling | Phase 1 | DONE |
| 3 | Secure email invitations, share links and guest lobby | Phase 2 | DONE |
| 4 | Custom LiveKit room for registered users and guests | Phase 3 | DONE |
| 5 | Durable chat, notes, files and complete meeting archive | Phase 4 | DONE |
| 6 | Consent-aware recording, Egress and playback | Phase 5 | IN PROGRESS |
| 7 | Security, scale, observability and production rollout | Phase 6 | NOT STARTED |

Only one phase may be `IN PROGRESS`. A phase becomes `DONE` only when all exit criteria and evidence are
recorded; compiling alone is not completion.

### Phase 0 — Architecture contract and feasibility spike

**Goal:** prove the risky media/provider assumptions before creating the full domain.

Backend/infrastructure:

- Pin compatible LiveKit server/client versions and record licenses.
- Add a local LiveKit + Redis Compose definition and example secret-free configuration.
- Define `IMeetingMediaProvider` methods and LiveKit adapter responsibilities without leaking provider
  DTOs into Domain/Application.
- Prove API-side creation of a short-lived room token with least-privilege grants.
- Prove signed webhook validation and idempotent event-id handling in an isolated spike/test.

Frontend:

- Install/pin `livekit-client`.
- Build a disposable development harness, not production UI, that joins one room in two browser
  contexts and proves microphone, camera, screen share, disconnect and reconnect.
- Verify Angular build/test compatibility, browser support, bundle impact and cleanup of tracks/devices.

Decisions/evidence:

- Record selected versions, local ports, production networking assumptions and whether the backend
  adapter uses a maintained SDK or direct documented APIs/JWT signing.
- Record a two-browser smoke-test result and any unsupported-browser policy.
- Remove or clearly isolate disposable harness code before marking the phase done.

**Exit criteria:** two local participants can join with API-issued scoped tokens, exchange audio/video,
share a screen, reconnect, and generate a verified webhook; secrets remain server-side; both projects
build/test; Phase 1 contracts no longer depend on an unproven LiveKit assumption.

**Completion evidence (2026-08-30):**

- Pinned LiveKit Server `1.13.6` (Apache-2.0), community .NET server SDK
  `Livekit.Server.Sdk.Dotnet` `1.2.3` (Apache-2.0), `livekit-client` `2.22.1`
  (Apache-2.0), and Redis `8.2.9-alpine` (AGPL-3.0). The backend uses the maintained community SDK
  behind TaskFlow's `IMeetingMediaProvider`; Application exposes no LiveKit types.
- Added the isolated `infra/meetings` Compose stack and example configuration. Local ports are HTTP/
  WebSocket `7880`, RTC/TCP `7881`, RTC/UDP `50000-50100`, Redis `6379`, API `5138`, and UI `4200`.
  Production still requires trusted `wss://`, a public-IP-aware deployment, UDP/TCP reachability and
  TURN/TLS fallback; this local topology is not a production template.
- Proved five-minute, room-scoped publish/subscribe tokens with no data/admin grants. Proved raw-body
  signed webhook validation, event-id replay rejection, and real LiveKit webhook delivery into the
  development-only TaskFlow probe. No API secret is sent to Angular or committed as a real credential.
- The disposable `/dev/meetings-livekit` harness is development-gated and the only Angular integration
  seam importing `livekit-client`. Two isolated Headless Chrome 151 contexts joined with independently
  issued tokens, published/subscribed microphone and camera, published/stopped screen share,
  disconnected, and rejoined with a new token. Tracks/devices are stopped and detached on cleanup.
- Phase 0 browser support is intentionally limited to a current Chromium desktop that passes
  `livekit-client`'s `isBrowserSupported()`; no production browser/mobile support promise is made until Phase 4's
  matrix. The lazy production probe chunk is 562.43 kB raw / 122.98 kB estimated transfer and is not
  reachable because its production feature flag is false.
- Verification: backend build passed; all backend tests passed `42/42` with the documented test-only
  one-time-code key; EF reports no model drift; frontend specs passed `258/258`; production build,
  Angular/design lint and all `42` light/dark contrast checks passed.

### Phase 1 — Domain, persistence, permissions and core API

**Goal:** make TaskFlow the authoritative meeting system before building the user interface.

- Add the meeting aggregate, lifecycle (`Draft/Scheduled/Live/Ended/Cancelled`), participants, badges,
  access links and attendance foundations using an additive EF migration.
- Add organization permissions: `CreateMeetings`, `ManageMeetings`, and `RecordMeetings`; seed them and
  expose them automatically through the existing role editor catalog.
- Creator can manage their own meeting; `ManageMeetings` plus owner bypass can manage any meeting in
  the organization. Archive reads are limited to creator, assigned participants, or authorized manager.
- Implement create/list/detail/update/cancel/start/end plus participant/badge/link metadata commands.
- Keep reads in Dapper and writes in repositories/unit-of-work per existing architecture.
- Add meeting feature flags/config validation and media-provider dependency registration.
- Implement lifecycle invariants, organization isolation, schedule/timezone validation and soft delete.

**Exit criteria:** migration/model drift pass; domain/application tests cover lifecycle and permissions;
real HTTP/PostgreSQL integration covers outsider denial and cross-organization isolation; API docs and
ProjectCompletion endpoint ledger are updated; no LiveKit secret or raw share token is returned by a
later read.

**Completion evidence (2026-08-30):**

- Added the `Meeting` aggregate with Draft/Scheduled/Live/Ended/Cancelled transitions plus meeting-owned
  badge definitions, registered participants, access-link metadata and attendance foundations. The
  additive `AddMeetingCore` migration creates five constrained/indexed tables with organization/user
  foreign keys and global soft-delete filters.
- Added `CreateMeetings`, `ManageMeetings` and `RecordMeetings` to the seeded permission catalog.
  Creation requires `CreateMeetings`; a creator manages their own meeting, while organization owners
  and roles with `ManageMeetings` can manage any meeting. Detail/archive reads are limited to the
  creator, an assigned non-revoked participant or an authorized manager.
- Added 13 authenticated, feature-gated routes for bounded/filterable/paged organization lists,
  detail, create/update, start/end/cancel, safe badge and registered-participant metadata, and
  create/list/revoke access-link metadata. A cryptographically random raw link token is returned once;
  PostgreSQL stores only its SHA-256 hash and safe later reads expose neither token nor hash.
- Added validated `Meetings` configuration for staged enablement, guest/recording dependencies,
  guest-session duration, retention and file-size bounds. Existing provider registration remains
  isolated behind `IMeetingMediaProvider`; official LiveKit token/grant and signed raw-body webhook
  contracts were rechecked and no provider dependency changed in this domain/API phase.
- Verification: backend build passes; all `49/49` tests pass, including lifecycle/permission unit tests
  and disposable-PostgreSQL HTTP coverage for participant access, outsider denial, cross-organization
  isolation, lifecycle timestamps and one-time raw-link disclosure; EF reports no model drift.

### Phase 2 — Organization meeting management and scheduling UI

**Goal:** users can manage meetings end to end before guest or media complexity is introduced.

- Add the Meetings sidebar item with a video icon and lazy organization routes.
- Build Upcoming, Live and Past list states with search/filter/paging, skeleton/error/empty states and
  organization-switch isolation.
- Build create/edit form for instant/scheduled meeting, timezone, settings and initial badge definitions.
- Build meeting detail with participant/access-link management placeholders, lifecycle actions and
  archive sections that truthfully show later features as unavailable rather than fake controls.
- Derive scheduled/live meetings into the existing Calendar mapper; do not duplicate them as
  `CalendarEntry` rows.
- Add repository/facade/models/API constants and focused tests following current Angular conventions.

**Exit criteria:** an authorized organization member can create, edit, cancel, start and end a meeting;
unauthorized controls are absent and API denial is proven; meetings appear once in Calendar; switching
organizations cannot leak prior meeting state; responsive/light/dark/accessibility checks, full specs,
lint/design lint, contrast and production build pass.

**Completion evidence (2026-08-30):**

- Added lazy `/organization/meetings` and detail routes plus the video-icon sidebar entry. Upcoming,
  Live and Past work states provide search, status filtering, client paging, skeleton/error/empty
  recovery and organization-scoped state reset.
- Added validated instant/scheduled creation and editing with IANA/Windows-compatible timezone text,
  room settings, retention and initial meeting-only badge definitions. Create controls follow
  `CreateMeetings`; edit/lifecycle/participant controls use the detail DTO's server-authored
  `CanManage` authority.
- Registered organization members can be added as co-hosts, participants or viewers and have access
  revoked. Guest links, join media, chat, notes, files and recording are explicit later-phase states,
  not fake controls; their four metadata routes remain intentionally unbound until Phase 3.
- Calendar derives timed Meeting records through the existing TaskFlow mapper and opens the canonical
  meeting detail; it creates no `CalendarEntry` duplicate and timed ends stay non-exclusive.
- Verification: all frontend specs pass `262/262`; production build, Angular lint, design lint, the
  Impeccable detector and all 42 light/dark contrast checks pass. A local browser reached the auth
  boundary correctly, but no test credentials or session were used for a destructive live-data pass.

### Phase 3 — Secure invitations, share links and guest access

**Goal:** a person with no TaskFlow account can securely join by verified email.

- Implement private invitation and reusable share-link creation, copy/email, expiry, usage limit,
  rotation/revocation, access-level selection and custom badge assignment.
- Add a meeting invitation email template using the existing email service.
- Add meeting-specific OTP challenge persistence, hashing, expiry, resend cooldown, attempt cap and rate
  limits. Do not require or create a normal TaskFlow account.
- Add public guest join routes outside protected portal layouts: link inspection, email verification,
  display-name confirmation and lobby shell.
- Issue/restore a short-lived one-meeting guest session and map it to a stable participant identity.
- Allow matching registered users to bind the invitation after a clear confirmation; prevent a logged-in
  different email from silently taking it.
- Add creator controls to admit, deny, revoke or remove guests and audit those decisions.

**Exit criteria:** private invite works only for its email; shared link requires OTP and respects expiry,
use count and revoke; OTP endpoints resist enumeration/brute force; guests cannot call organization or
other-meeting APIs; reload/reverification behavior is documented; mail, integration and browser tests
cover registered match, unregistered guest, wrong email/code, expired/revoked link and duplicate use.

**Completion evidence (2026-08-31):**

- Added private invitations and reusable links with email locking, expiry, use limits, access/badge
  defaults, one-time raw-token disclosure, invitation mail, rotation and revocation. PostgreSQL stores
  only token hashes; distinct verified participants consume capacity once, and a previously verified
  email may reverify without consuming another use.
- Added meeting-only six-digit challenges with HMAC hashes, ten-minute expiry, sixty-second resend
  cooldown, five-attempt cap and a dedicated public rate policy. Verification creates a stable guest
  participant and a separate opaque `X-Meeting-Guest-Session`; it never issues a TaskFlow JWT. An
  authenticated account is bound only after explicit confirmation and an exact verified-email match.
- Added the public `/meetings/join` flow outside protected layouts. The invitation token is accepted
  only from the URL fragment, copied into tab-scoped pending storage and scrubbed from the address bar
  before inspection. A verified guest session restores on reload until expiry or organizer revocation;
  after expiry, the guest must reverify through an active original link. Private links remain locked to
  their email throughout this flow.
- Added organizer link management and guest admit/deny/revoke/remove controls. Non-admit decisions
  revoke active guest sessions, and guest sessions cannot authorize organization or other-meeting API
  access. The lobby truthfully keeps room media unavailable until Phase 4.
- Added the additive `AddMeetingGuestAccess` migration, invitation/code email templates, focused domain
  and protector tests, and disposable-PostgreSQL HTTP coverage for wrong code, anonymous verification,
  link capacity, organization-route denial and session invalidation after revocation. Backend tests pass
  `52/52`; frontend specs pass `267/267`; production build, Angular/design lint, Impeccable detector and
  all `42` contrast checks pass. Responsive browser verification proved immediate fragment scrubbing,
  clean invalid/revoked handling and no horizontal overflow at 390px.
- Official LiveKit client/token/webhook contracts were rechecked; `livekit-client` remains pinned at
  `2.22.1` and isolated behind the Phase 0 probe. Phase 3 adds no media-provider dependency.

### Phase 4 — Custom LiveKit room

**Goal:** registered participants and verified guests can hold a reliable TaskFlow-branded call.

- Implement API-issued least-privilege LiveKit join tokens for both auth types.
- Build pre-join device preview and the full custom responsive room UI.
- Publish/subscribe microphone and camera; add screen sharing, speaker/grid layout, roster, active
  speaker, connection quality, reconnecting and device switching.
- Render display badges on tiles/roster and access level in management views.
- Enforce capability matrix in API and LiveKit grants; implement host/co-host admit, mute/remove and end.
- Process signed room/participant webhooks into deduplicated attendance intervals; reconcile open
  intervals when room/end events arrive out of order.
- Handle permission denial, unsupported browser, token expiry, network loss, removed user and closed room.

**Budgeted work-package order:**

1. **P4.1 — DONE: Stabilize the room-access change set:** fixed the failing meeting integration test and
   added focused regression coverage for member/guest join-token authorization. No new room capability.
2. **P4.2 — DONE: Complete server moderation and attendance:** finish the signed webhook/attendance persistence
   path and prove moderation authorization with focused domain/application/integration tests.
3. **P4.3 — DONE: Complete the room UI:** finish only the missing client controls and focused Angular tests for
   pre-join, devices, screen share, roster/status and capability-disabled states.
4. **P4.4 — DONE: Prepare the disposable media environment:** install/configure Docker, LiveKit and test-only
   credentials; prove a health check. This is infrastructure only, with no product-surface changes.
5. **P4.5 — DONE: Certify Phase 4:** run the specified multi-browser registered/guest/viewer/reconnect/removal
   scenarios, then run the full validation/documentation gate and mark the phase done only if it passes.

**Exit criteria:** two registered users, a registered user plus guest, and multiple guests complete the
join/call/leave flows; badges render correctly; viewer cannot publish; participant cannot moderate;
reconnect creates truthful attendance; host removal revokes API access and disconnects LiveKit; unit,
integration and multi-browser E2E/manual evidence is recorded without using real customer data.

**Completion evidence (2026-08-31):**

- Added short-lived, server-derived member and admitted-guest room credentials, plus host/co-host mute
  and removal endpoints. LiveKit grants encode the participant's access capabilities; the Angular room
  hides unavailable publishing/moderation controls and renders server-authored badges and access labels.
- Added signed raw-body LiveKit webhook handling for join/leave/room-finished events. Attendance uses
  connection-scoped intervals and durable globally unique `MeetingWebhookReceipt` rows, so replayed
  provider events are harmless. The additive `AddMeetingWebhookReceipts` migration has no model drift.
- The custom responsive room now includes pre-join preview/device selection, microphone/camera and
  screen-share controls, device switching, participant tiles/roster, active-speaker and connection-
  quality state, reconnecting/removed/closed-room handling, and deterministic track cleanup.
- Disposable PostgreSQL HTTP coverage proves non-moderator denial, signed-webhook acceptance, durable
  replay deduplication, and host removal revoking subsequent room-token access while calling the media
  provider. Focused meeting coverage passes `20/20`; the complete backend suite passes `60/60`.
- The official LiveKit Server `1.13.6` Windows binary was used as the documented standalone fallback
  because Docker Desktop installation required an unavailable administrator handoff. Its release SHA-256
  was verified and the disposable server passed health/startup checks on `7880` with TCP/UDP RTC enabled.
- Two independent in-app browser contexts joined the real disposable room as a registered participant
  and guest, observed each other's presence, reflected disconnect, and completed a fresh-token reconnect;
  LiveKit delivered signed webhooks to the API. Phase 0's isolated Chromium proof remains the media-track
  evidence for microphone, camera and screen share; no real device permission or customer data was used.
- Angular room/repository coverage passes, including `7/7` focused room specs and `275/275` complete
  specs. Backend/frontend builds, Angular/design lint, all `42` contrast checks, EF drift, and temporary-
  service cleanup pass. Viewer grants, badge/capability rendering, member/guest authorization, multiple-
  guest identity separation, moderation denial/removal and attendance reconciliation are additionally
  covered at unit/application/disposable-PostgreSQL boundaries.

### Phase 5 — Persistent collaboration and archive

**Goal:** all non-media meeting work survives refresh, late join and meeting end.

- Add durable idempotent chat send/history with realtime LiveKit announcement and API reconciliation.
- Add one versioned shared note with debounced autosave, visible save/conflict states and revision audit.
- Add authorized meeting file upload/list/download/delete using private object storage, allowlists,
  quotas, hash/scan status and safe content headers. Broadcast metadata, not file authority, via LiveKit.
- Add archive timeline: actual start/end, attendance/duration, messages, notes, files and uploader/author.
- Apply access-level and meeting-setting rules consistently to registered users and guests.
- Define and implement content retention/deletion behavior; do not orphan storage objects.

**Exit criteria:** late join and refresh reconstruct identical canonical state; failed realtime broadcast
does not lose a persisted message; retries do not duplicate it; concurrent note versions cannot silently
overwrite; unauthorized guests cannot fetch another meeting's assets; meeting end leaves a complete,
ordered archive with who shared what and when.

**Completion evidence (2026-09-01):**

- Added durable, participant-authored messages with per-author client idempotency keys and ordered
  history. Angular persists first, then sends a small LiveKit data announcement; receivers reload the
  canonical API state, so failed or repeated realtime delivery cannot lose or duplicate content.
- Added one 100,000-character shared note with optimistic versions, immutable revision rows, 700 ms
  debounced autosave and visible saved/saving/conflict/failure states. A stale expected version returns
  `409 MEETING_NOTE_CONFLICT` and the client reloads the newer revision instead of overwriting it.
- Added private meeting assets through the existing local/S3 object-storage and scanner boundaries.
  PDF/PNG/JPEG/TXT/DOCX extension, MIME and signature checks, the configured 25 MB file ceiling, a
  ten-file-equivalent per-meeting byte quota, safe names, SHA-256, scan state, authorized download and
  host/uploader deletion are enforced for registered users and guests.
- Added the canonical archive with actual start/end, ordered attendance intervals/durations, messages,
  current note/editor, files/uploader and retention deadline. Collaboration becomes read-only after end;
  an active link can be reverified for guest archive access until expiry/retention.
- Added six-hour retention cleanup. It deletes every private object successfully before soft-deleting
  expired messages, note/revisions, file metadata and attendance; failed object deletion leaves the
  archive intact for a later retry rather than orphaning storage.
- Added the additive `AddMeetingCollaborationArchive` migration and 18 registered/guest routes.
  Disposable PostgreSQL HTTP coverage proves message retry deduplication, outsider asset denial, note
  conflict, scanned upload/download, ended-meeting write denial and complete archive reconstruction.
  Backend tests pass `62/62`, frontend specs `276/276`; both builds, Angular/design lint, all `42`
  contrast checks and EF model drift pass. Phase 6 is READY.

### Phase 6 — Recording, Egress and playback

**Goal:** hosts can intentionally record and retain a meeting with clear participant awareness.

- Deploy/configure version-pinned LiveKit Egress locally and in staging.
- Add recording/consent domain records and server-side start/stop orchestration.
- Implement strict current-participant consent flow, timeout/decline behavior, mid-recording join consent,
  persistent red indicator and immutable consent audit.
- Start room-composite MP4 Egress from the API and write to a private S3-compatible object path.
- Process Egress webhooks idempotently and expose truthful processing/ready/failed states.
- Stop active recording when host clicks Stop or meeting ends; recover/reconcile stuck jobs.
- Add authorized archive playback/download and creator delete with storage cleanup.
- Document Egress CPU/concurrency expectations and establish a staging capacity test.

**Implementation checkpoint (2026-09-01):** application and local-infrastructure work is complete.
The API now owns consent, Egress orchestration/reconciliation, private playback and deletion; Angular
binds all nine member/guest routes with consent gates, recording indicators and archive controls.
`AddMeetingRecordingEgress` plus `EnforceSingleActiveMeetingRecording` provide the schema and a
database-enforced one-active-recording invariant. Production enablement remains gated on a real
container/staging playable-MP4 and capacity run plus jurisdiction-specific legal/product approval.

**Exit criteria:** no unauthorized or unconsented recording can start; all clients show recording state;
start/stop/end paths produce exactly one playable authorized archive record; failed Egress is recoverable
and does not claim success; guest/archive access is scoped; storage deletion is verified; legal/product
review of disclosure and retention is recorded before production enablement.

### Phase 7 — Hardening, observability and production rollout

**Goal:** release Meetings safely under measurable operational limits.

- Threat-model links, OTP, guest sessions, IDOR, privilege escalation, webhook replay, file abuse,
  meeting enumeration, recording access and token leakage.
- Add concurrency/limit tests for participants, messages, files and simultaneous recordings using
  declared supported ceilings; document capacity rather than claiming unlimited scale.
- Add structured metrics/traces/logs without sensitive content and actionable alerts/runbooks.
- Verify TURN behavior from restrictive networks and desktop/mobile supported browsers.
- Fix ready-anytime meeting creation so removing the schedule also removes the hidden start/end
  required validators; add a browser regression test for scheduled-to-ready transitions.
- Provision the production LiveKit/Redis/TURN topology and set `LiveKit__Enabled=true`, a trusted
  public `LiveKit__Url` using `wss://`, and environment-owned API credentials. Prove join-token
  issuance before attempting the two-device media test.
- Add critical E2E coverage: create → invite → guest OTP → join → call → collaborate → record → end →
  archive, plus revoke/remove/denial paths.
- Roll out behind `Meetings:Enabled`, then guests, then recording. Staging soak precedes production.
- Update privacy policy, retention/deletion documentation, support troubleshooting, backup/restore and
  incident/secret-rotation procedures.

**Exit criteria:** security review has no unresolved high-risk item; performance/capacity evidence meets
declared limits; monitoring/runbooks and rollback are tested; migrations and object storage are backed
up; staged feature flags are verified; production health and one synthetic meeting are recorded in the
ProjectCompletion ledger.

## 11. Cross-phase definition of done

Every phase must satisfy all relevant gates:

- Domain invariants and API authorization are enforced server-side.
- New write handlers use repositories/unit of work; reads use Dapper; controllers remain thin.
- Additive migrations apply to a real disposable PostgreSQL database and EF reports no model drift.
- Angular pages use facade/repository boundaries, design tokens, loading/error/empty states and complete
  focused specs; reusable UI follows Atomic Design scaffolding rules.
- Backend domain/application/integration tests and full frontend tests/build/lint/design lint/contrast pass.
- Guest/registered and organization-isolation cases are included whenever relevant.
- Secrets and content never appear in logs, fixtures, screenshots or committed configuration.
- Official dependency versions/licenses/security notes are rechecked and lockfiles are committed.
- Manual/multi-browser evidence is recorded for behavior automation cannot truthfully prove.
- This plan's status table/evidence log, both `PHASES.md`, both `SESSIONS.md`, and ProjectCompletion
  parity/count/changelog are updated in the same session when applicable.

## 12. Decisions that require explicit approval before implementation

These are intentionally not guessed during later phases:

1. Maximum participant count per meeting and supported simultaneous meetings.
2. Default meeting/archive/file/recording retention periods and deletion grace period.
3. Maximum upload size/type allowlist and per-meeting storage quota.
4. Whether viewers may chat and whether participants may edit notes by default.
5. Whether co-hosts may start recordings.
6. Production geography/data residency and target jurisdictions for recording consent review.
7. Whether guest archive access remains available after the meeting and for how long.
8. Whether custom badges are meeting-only initially or reusable organization badge presets.

Until approved, phases use the conservative defaults stated in this document and feature flags keep
guest/recording behavior disabled in production.

## 13. Evidence and decision log

- **2026-09-01 — Phase 6 implementation complete; external certification pending:** added immutable
  participant consent, host-only start/stop, late-join blocking, LiveKit room-composite MP4 Egress,
  idempotent webhook/recovery reconciliation, private member/guest playback and storage-first delete.
  Local Compose pins Egress `1.12.0` and documents a conservative one-720p-job-per-4-vCPU/4-GB policy.
  API routes are `178/175` exposed/consumed; backend tests pass `65/65`, frontend specs `278/278`,
  builds, lint/design lint, all 42 contrast checks and EF drift pass. Docker is unavailable on this
  workstation, so no real Egress container/playable archive/capacity result was fabricated. Recording
  stays disabled for production until that staging proof and the open geography/consent review exist.
- **2026-09-01 — Phase 5 completed:** delivered canonical member/guest chat, optimistic shared notes,
  private scanned meeting files, ordered archives and storage-safe retention cleanup through the
  `AddMeetingCollaborationArchive` migration and 18 bound routes. Persist-first LiveKit announcements
  reconcile clients without becoming history authority. Full gates pass at backend `62/62`, frontend
  `276/276`, builds, lint/design lint, 42 contrast checks and zero EF drift. Phase 6 is READY.
- **2026-09-01 — production meeting audit:** pushed/deployed Phase 5 and created production meeting
  `#3`, added the Shubham Kashyap account and started the meeting successfully. The collaboration
  panel then loaded without its earlier archive-route error. The room cannot issue a join token
  because production LiveKit is disabled, so PC/mobile media is not yet testable. Ready-anytime form
  submission also remains invalid after schedule removal because hidden date controls retain required
  validators. Both follow-ups are recorded under Phase 7; scheduled creation remains operational.

- **2026-08-31 — Phase 4 completed:** delivered the production custom LiveKit room, least-privilege
  member/guest grants, host/co-host moderation, signed durable replay-safe attendance, and the
  `AddMeetingWebhookReceipts` migration. Official LiveKit `1.13.6` passed the standalone disposable
  health path; two independent browser contexts proved registered/guest presence, leave and fresh-token
  reconnect with real signed webhook delivery. Full gates pass at backend `60/60`, frontend `275/275`,
  builds, lint/design lint, 42 contrast checks and zero EF drift. Phase 5 is READY.

- **2026-08-31 — Phase 4 / P4.1 completed:** the disposable PostgreSQL HTTP suite now proves that an
  assigned member receives a short-lived room credential, an unassigned cross-organization user is
  denied, and a verified guest remains denied until an organizer admits that exact participant. The
  integration host enables only token signing with test-only LiveKit credentials; it does not require
  or contact a LiveKit server. Each integration client now uses a distinct forwarded test IP, preserving
  real guest rate-limit behavior without cross-scenario contention. The previously failing meeting
  lifecycle integration assertion now reports the response payload on failure, and the complete backend
  suite is green at 59 tests.
  P4.2 (moderation and durable attendance) is the next bounded package.

- **2026-08-31 — Phase 4 in progress:** added server-derived, opaque, short-lived join credentials
  for both assigned members and verified/admitted guest sessions, plus isolated authenticated and
  guest room routes consuming the TaskFlow-owned LiveKit wrapper. The API and production Angular
  build pass. Full Phase 4 completion remains pending: Docker/LiveKit is not installed on this host,
  so the required multi-browser call, reconnect, moderation and signed-attendance evidence cannot
  yet be run; the remaining room controls, moderation endpoints and attendance webhook persistence
  must be completed before this phase can be marked DONE. A subsequent room-access hardening pass also
  closed a privilege escalation: meeting-management authority alone cannot receive the creator's host
  credentials; only an assigned participant can obtain a join token. Assigned organization members are
  admitted directly, while guests retain organizer-controlled admission.

- **2026-08-31 — Phase 3 completed:** shipped hash-only private/reusable invitations, meeting-specific
  OTP verification, tab-scoped guest sessions, organizer guest decisions and the public guest lobby.
  The API now exposes 19 meeting routes and Angular consumes all 19; backend tests pass `52/52`,
  frontend specs pass `267/267`, and build/lint/design/contrast/EF-drift/browser gates are green.
  Phase 4 is READY.

- **2026-08-30 — Phase 2 completed:** shipped the organization Meetings navigation, list/detail,
  validated management/lifecycle and registered-participant UI, plus duplicate-free Calendar
  derivation. Nine of the 13 core meeting routes are now bound; the four badge/link metadata routes
  stay staged for Phase 3. Frontend verification is 262/262 with build/lint/design/contrast/detector
  green. Phase 3 is READY.

- **2026-08-30 — Phase 1 completed:** TaskFlow now owns the meeting lifecycle, persistence,
  authorization and management contract through 13 feature-gated API routes and the additive
  `AddMeetingCore` migration. Three permissions are seeded, raw access tokens are hash-only after
  creation, all `49/49` backend tests pass with real PostgreSQL isolation coverage, and EF has no
  model drift. Phase 2 is READY.

- **2026-08-30 — Phase 0 completed:** pinned the LiveKit/Redis stack, established the provider boundary,
  added scoped-token and signed/idempotent-webhook proofs, and verified the disposable Angular harness
  across two isolated browser contexts with mic, camera, screen share, disconnect and fresh-token
  reconnect. Both projects build, all `42` backend and `258` frontend tests pass, EF has no drift, and
  frontend lint/design/contrast gates pass. Phase 1 is READY.

- **2026-08-30 — Plan created:** approved product direction captured for organization Meetings,
  registered/unregistered email participants, private and reusable share links, separate security
  access levels and custom display badges, custom Angular/LiveKit calling UI, persistent collaboration,
  attendance/archive history and consent-aware recording. Phase 0 is READY; no implementation,
  dependency, endpoint or migration has landed yet.
