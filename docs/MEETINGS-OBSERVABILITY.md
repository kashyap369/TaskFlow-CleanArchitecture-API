# Meetings observability — signals, alerts and what to do about each one

**Status:** Phase 7 / P7.4, written 2026-09-05.

**What this is.** Every signal the Meetings feature emits, the rule that watches it, and the runbook
for each rule. It exists because Meetings has a specific failure mode: *the screens keep looking
fine*. Attendance stops being written and the archive still renders. A recording never starts and
the host sees a normal panel. Join tokens are refused and the only person who knows is the one
staring at a lobby. Nothing here is about dashboards for their own sake — each rule below was chosen
because the failure it catches is otherwise invisible until someone complains.

Read it with two neighbours: [MEETINGS-CAPACITY.md](MEETINGS-CAPACITY.md) says what the declared
ceilings are (the `capacity_refusals` rule points at it), and
[infra/meetings/RUNBOOK.md](../infra/meetings/RUNBOOK.md) is the production triage for a meeting
that is broken right now (the media and webhook rules point at it).

---

## 1. The privacy contract

This is the part to read before adding a signal. Telemetry is the most widely copied data in a
system: it goes to logs, dashboards, screenshots, tickets and, in a scaled deployment, to a
third-party collector. A meeting's contents are the most sensitive data TaskFlow holds. So the rules
are strict, and they are enforced by a test
(`MeetingObservabilityTests.NoMeetingSignalCarriesMeetingContent`) that drives real handlers with
deliberately distinctive values and fails if any of them reaches a tag.

**Never in a metric tag, a log field or a span:**

- an email address, a guest access-link token, a guest session token or a join token
- a LiveKit room name, a participant identity or a display name
- a meeting title, description, chat message, note text or file name
- a client IP address

**Never a metric tag, even though it is not secret:** a meeting id, participant id, organization id
or a concrete URL path. Not for privacy but for cardinality — one time series per meeting takes a
metrics backend down, and an unmatched path lets any caller mint series by inventing URLs. The edge
middleware therefore tags the **route template** (`api/meeting/{meetingId:int}/messages`) and buckets
anything that matched no endpoint as `unmatched`.

**Where an identifier is allowed:** on the span (`taskflow.meeting.id`), because a span belongs to
one trace an engineer already has open; and in a log line for the one request that needs it. Not in
the shared metric stream.

Consequences of these rules that are worth stating rather than discovering:

- You cannot answer "which meeting is failing" from metrics alone. You find *that* something is
  failing from metrics, then find *which* from the trace or the log line. That is the intended
  trade, not a gap.
- Refusal **codes** are tags (`MEETING_ROOM_ACCESS_REVOKED`), refusal **messages** are not. Codes are
  a fixed vocabulary; messages interpolate configured numbers and would grow the tag space.

---

## 2. The signals

All instruments are on the .NET meter **`TaskFlow.Meetings`**, defined in one place —
`TaskFlow.Application/Common/Observability/MeetingTelemetry.cs`. Traces use the activity source of
the same name.

| Metric | Type | Key tags | Emitted from |
|---|---|---|---|
| `taskflow.meetings.requests` | counter | route template, method, status code, status class, actor | `MeetingObservabilityMiddleware` |
| `taskflow.meetings.request.duration` | histogram (ms) | same | `MeetingObservabilityMiddleware` |
| `taskflow.meetings.join.tokens` | counter | actor, outcome (`issued`/`refused`), refusal reason | join-token handlers (member and guest) |
| `taskflow.meetings.guest.verifications` | counter | stage (`inspect`/`request_code`/`verify`), outcome, reason | guest access handlers |
| `taskflow.meetings.webhooks` | counter | outcome (`accepted`, `duplicate`, `ignored`, `rejected_signature`, `rejected_processing`) | webhook controller and handler |
| `taskflow.meetings.recordings` | counter | event (`requested`, `consent_pending`, `started`, `start_failed`, `roster_unavailable`, `stopped`) | recording handlers |
| `taskflow.meetings.capacity.refusals` | counter | limit (the refusal code) | `MeetingCapacityRules` |
| `taskflow.meetings.media.calls` | counter | operation, outcome | `LiveKitMeetingMediaProvider` |
| `taskflow.meetings.media.duration` | histogram (ms) | operation, outcome | `LiveKitMeetingMediaProvider` |

Three details that decide whether these numbers mean what you think:

- **`status_class` splits `denied` (401/403) out of `client_error`.** A run of refusals on meeting
  routes is the shape of someone working over a leaked link; diluting it with ordinary validation
  failures would make it unalertable.
- **Media calls are measured *after* the enablement guard.** A deployment with `LiveKit:Enabled`
  false reads as "no calls", not "every call failing" — that state is readiness's answer, not this
  counter's.
- **Webhook `ignored` is not a fault.** Several environments can share one LiveKit server, so
  deliveries naming rooms this deployment does not own are normal traffic. Only outcomes prefixed
  `rejected` count against the alert.

### Logs

The edge middleware writes structured log lines with `Route`, `Method`, `StatusCode`, `DurationMs`,
`Actor` and `TraceId` — never a path, a body or a header. It logs at:

- **Warning** for 5xx and for any request at or above `Meetings:SlowRequestMilliseconds` (default
  1500). Joining and sending chat are interactive; a two-second success has already failed the
  person waiting, and nothing else would say so.
- **Information** for refusals (401/403/429) — the abuse trail, where the rate is what matters — and
  for mutations, which is the audit line.

Rejected webhooks and unreadable rosters log at Warning/Error from their own call sites, because
both are silent to every user.

---

## 3. Where to read them

**With a metrics collector.** Scrape the `TaskFlow.Meetings` meter through OpenTelemetry and
implement §4 as alert rules there. The thresholds transfer unchanged; that is why they are written
as a table rather than embedded in a dashboard.

**Without one — which is the production deployment today.** `GET /admin/meetings/health` (AdminOnly)
returns a rolling in-process window: one-minute buckets over the last hour, every rule evaluated,
plus raw 5 / 15 / 60-minute counts per signal. The Angular admin page renders it at
`/admin/settings` → **Meetings health**, next to Meetings readiness.

Two limits of that endpoint, both reported in its own response rather than left for you to discover:

- **It is per instance.** The window belongs to the process that answered the request. A scaled-out
  deployment needs a collector; a single-container one does not.
- **It resets on restart.** `fullyObserved` is false until the process has been collecting for a
  full hour, and the UI says so. "No failures in the last hour" from a ten-minute-old process is a
  claim the data cannot support.

Readiness and health answer different questions and both are needed: **readiness** is "is this
process configured to run a meeting", **health** is "is it actually working".

---

## 4. The alert rules

Every rule is evaluated in `MeetingHealthSnapshot`, and the anchor in the last column is the
`runbook` field the API returns — the panel shows it verbatim so the operator lands here.

| Rule | Severity | Fires at | Window | Meaning |
|---|---|---|---|---|
| [`media_calls_failing`](#media_calls_failing) | Critical | 3 failed calls | 5 min | LiveKit is unreachable or credentials are wrong |
| [`server_errors`](#server_errors) | Critical | 1 × 5xx | 5 min | A meeting route threw |
| [`recording_failures`](#recording_failures) | Critical | 1 event | 15 min | A recording never started, or consent was asked without a roster |
| [`webhooks_rejected`](#webhooks_rejected) | Critical | 5 rejections | 15 min | Attendance and recording state are going stale |
| [`join_tokens_refused`](#join_tokens_refused) | Warning | 10 refusals | 5 min | People are being kept out of rooms |
| [`guest_verification_failures`](#guest_verification_failures) | Warning | 20 failures | 15 min | Code guessing against a link |
| [`capacity_refusals`](#capacity_refusals) | Warning | 5 refusals | 15 min | A declared ceiling is being hit |
| [`throttling`](#throttling) | Warning | 25 × 429 | 15 min | Rate limits are biting |

Thresholds are deliberately conservative and, like the capacity numbers, are engineering defaults
rather than owner-approved figures. Tune them against real traffic once Phase 7 rollout produces
some; an alert that fires every day teaches people to ignore it, which is worse than no alert.

---

## 5. Runbooks

### media_calls_failing

**What it means.** Calls from the API to LiveKit are failing. Moderation (remove, mute), the live
roster and recording start/stop all go through those calls, so hosts lose control of the room and no
recording can begin. Joining may still appear to work — a join token is signed locally and does not
touch LiveKit — which is exactly how this failure hides.

**First checks, in order.**

1. `/admin/settings` → **Meetings readiness**. If it is not `Ready`, this is a configuration
   problem, and [RUNBOOK.md §1.A](../infra/meetings/RUNBOOK.md) covers the case where the platform
   saved variables the container never received.
2. `curl https://livekit.inksphere.space/` → expect `OK`. If not, the LiveKit service is down.
3. Read the failing **operation** tag on the health panel. `start_recording` alone points at Egress
   or object storage; every operation failing points at the server, the URL or the credentials.
4. LiveKit logs for authentication failures — a rotated API key/secret pair that reached one side
   only produces exactly this.

**If it was a credential rotation:** rotating LiveKit credentials requires updating the API *and*
the LiveKit server together, then restarting the API so it reloads them. Confirm with readiness's
API-key fingerprint, which is a hash of what the running process holds.

### server_errors

**What it means.** A meeting request threw an unhandled exception. The threshold is one because
meeting flows strand people: a 500 during join leaves someone in a lobby with no route forward and
no message worth reading.

**What to do.** Find the request in the logs by `TraceId`, and use `Route` to see which endpoint —
the route template is in the log line. Then read the span, which carries `taskflow.meeting.id`, to
find the meeting without needing it in the metric.

### recording_failures

**What it means.** One of two things, both serious:

- `start_failed` — consent was collected, everyone believes the meeting is being recorded, and no
  recording exists. The recording row is marked failed, but nobody in the room is told.
- `roster_unavailable` — the API could not read who was in the call, so it **refused** to request
  consent. That refusal is correct (see the P7.2 finding: consent collected from a stale attendance
  list records people who were never asked), but it means recording is unavailable until the media
  stack answers.

**What to do.** Both are downstream of the media stack: work `media_calls_failing` first. For
`start_failed` specifically, check Egress separately — it is its own service with its own storage
credentials ([infra/meetings/RECORDING.md](../infra/meetings/RECORDING.md)). Tell the affected hosts
explicitly; the UI will not, and a host who believes they have a recording will act on that belief.

### webhooks_rejected

**What it means.** LiveKit is delivering webhooks and TaskFlow is refusing them. Nothing visible
breaks: rooms work, chat works, the archive renders. But attendance intervals, room lifecycle and
recording completion are all written by webhooks, so the database is quietly diverging from reality
— and, because attendance feeds it, so is the audit trail.

**What to do.**

1. `rejected_signature` is the common case: the API secret does not match, or the webhook URL points
   at a different environment. Compare readiness's API-key fingerprint with the LiveKit
   configuration. Also check clock skew if signatures are time-bound.
2. `rejected_processing` means the handler threw — read the logs by `TraceId`; this is a code fault,
   not a configuration one.
3. Confirm from the LiveKit side that the webhook URL is the current API host; a redeploy that
   changed the domain leaves the old one configured.

**Do not** treat `ignored` as part of this. It counts deliveries for rooms this deployment does not
own, which is normal when environments share a LiveKit server.

### join_tokens_refused

**What it means.** People are asking to join and being turned away. Ones and twos are ordinary — a
meeting that already ended, a host who revoked someone. A run means something systemic.

**What to do.** Read the refusal reason in the logs (it is on the measurement, not the series key):

- `MEETING_ROOM_NOT_LIVE` in volume — participants have the link and the host has not started, or a
  meeting ended while people were still trying. Usually a communication problem, not a fault.
- `MEETING_ROOM_ACCESS_REVOKED` / `MEETING_GUEST_SESSION_INVALID` in volume — a link was revoked or
  rotated and the guests are still trying. Expected right after a revocation, which by design also
  ejects them; if it does not subside, someone is retrying a link they should not have.
- `MEETING_RECORDING_CONSENT_REQUIRED` in volume — a consent request is open and people cannot get
  past it. Check whether a recording is stuck in `PendingConsent`.

### guest_verification_failures

**What it means.** Six-digit codes are being submitted and failing. This is the only unauthenticated
way into a meeting, and this rate is what a guessing attempt looks like from the outside.

**What to do.** The path is already defended — codes expire in 10 minutes, allow 5 attempts, and the
`meeting-guest-verify` rate limit is 12/minute per address — so this is an *investigate*, not a
*panic*. Confirm from the logs whether failures cluster on one link. If they do, revoke or rotate
that access link: revocation now also kills the guest sessions already issued from it and ejects
those guests from the live room (P7.2). Then tell the organizer, because a leaked link is usually a
forwarded email.

### capacity_refusals

**What it means.** A declared ceiling refused a write. The `limit` tag names which one.

**What to do.** Read [MEETINGS-CAPACITY.md](MEETINGS-CAPACITY.md) and decide whether the ceiling is
wrong for real use or whether something is abusing it. Every ceiling is `Meetings:Max*`
configuration validated at startup, so raising one is a configuration change and a restart — but
raise it deliberately: the recording ceiling in particular reflects what the deployment's CPU can
actually encode, and raising it produces failed recordings rather than more recordings.

### throttling

**What it means.** Meeting requests are being rate limited. Guest verification, guest traffic, guest
uploads, webhooks and collaboration writes each have their own budget.

**What to do.** Check the actor tag. `guest` throttling during a large meeting may mean the budget
is genuinely too tight (the per-session budget is 180/min, keyed by a hash of the session token, so
a shared NAT is no longer one bucket). `webhook` throttling is more serious — dropped webhooks
become the `webhooks_rejected` failure above, one step removed.

---

## 6. What this does not cover

Stated plainly, because an observability document that implies full coverage is worse than none:

- **Client-side media quality.** Packet loss, jitter, ICE failures and TURN behaviour live in the
  browser and in LiveKit, not in the API. Nothing here would show a call that connected and sounded
  terrible. That is Phase 7's TURN/browser verification work, and LiveKit's own metrics.
- **Anything cross-instance.** The in-process window is per container.
- **Long-term trends.** One hour, in memory, gone on restart. Capacity planning needs a collector.
- **Whether a meeting was any good.** These are fault signals. No engagement or usage analytics are
  collected, and adding them would need a privacy decision, not just a counter.
