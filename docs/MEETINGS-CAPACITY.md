# TaskFlow Meetings — Declared capacity

> **Phase 7 / P7.3.** TaskFlow states limits it can defend instead of implying unlimited scale.
> Every ceiling here is **refused server-side**, not merely hinted at in the UI, and every one is
> pinned by a test. Read this before changing a `Meetings:Max*` value, and before telling anyone
> what a TaskFlow meeting can hold.
>
> These numbers are the conservative defaults [MEETINGS.md](MEETINGS.md) §12 prescribes until the
> owner approves real ones (§12 items 1 and 3). They are configuration, so a deployment can raise
> them — but nothing here has been load-tested at a higher value, and raising a number is a claim
> the deployment then has to meet.

## 1. The ceilings

All of them live in the `Meetings` configuration section and are read through `IMeetingPolicy`.
The running values are visible to an operator at **`/admin/settings` → Meetings readiness →
Declared capacity**, so nobody has to read a config file to answer "why was that refused".

| Setting | Default | Scope | Refused with |
|---|---|---|---|
| `MaxParticipantsPerMeeting` | 50 | one meeting's roster, host included | `MEETING_PARTICIPANT_LIMIT_REACHED` |
| `MaxConcurrentLiveMeetingsPerOrganization` | 10 | one organization, meetings in `Live` | `MEETING_CONCURRENT_LIMIT_REACHED` |
| `MaxConcurrentRecordings` | 1 | **whole deployment** | `MEETING_RECORDING_CAPACITY_REACHED` |
| `MaxMessagesPerMeeting` | 5 000 | one meeting | `MEETING_MESSAGE_LIMIT_REACHED` |
| `MaxAssetsPerMeeting` | 100 | one meeting | `MEETING_FILE_COUNT_LIMIT_REACHED` |
| `MaxFileBytes` | 25 MB | one upload | `MEETING_FILE_TOO_LARGE` |
| `MaxStorageBytesPerMeeting` | 250 MB | one meeting's files | `MEETING_FILE_QUOTA_EXCEEDED` |
| `GuestAccessRecordRetentionDays` | 30 | spent guest sessions/OTP challenges | not a refusal — a purge |

Startup validation refuses a nonsensical ceiling (`DependencyRegistration`), including
`MaxStorageBytesPerMeeting < MaxFileBytes`, which would make every upload impossible.

## 2. Where each one is enforced, and why there

- **Participants** — `Meeting.EnsureParticipantCapacity`, called on meeting creation, participant
  add, and guest verification. The roster is the *only* gate on room size, because a join token is
  issued to assigned participants and nobody else; there is deliberately no second count against the
  provider's live roster at join time, which would add a network dependency to every join.
  Removed, revoked and denied participants do **not** hold a seat: they cannot return without a new
  organizer decision, and holding their seats would slowly shrink every long-running meeting.
  A guest whose email already holds a seat is re-admitted rather than counted twice, so reconnecting
  from a second browser is never refused for capacity.
- **Simultaneous meetings** — `StartMeetingCommandHandler`, not creation. Scheduling ten meetings
  for the same hour is fine; holding more than the declared number of *live* rooms is not.
- **Simultaneous recordings** — `RequestMeetingRecordingCommandHandler`, before consent is requested
  from anyone. Egress is CPU-bound and shared across organizations (one 720p room-composite job per
  4 vCPU / 4 GB worker — see [RECORDING.md](../infra/meetings/RECORDING.md)), so this is a
  deployment ceiling. Asking a room to consent and *then* failing to start would leave people
  believing they had been recorded.
- **Messages** — checked only once a send is known not to be a retry, so a client retrying a message
  that already landed still succeeds at the ceiling rather than being told it was lost.
- **Files** — count and total bytes, checked before the stream is read. The byte quota replaces an
  implicit `MaxFileBytes * 10` that used to live inside the upload handler; the default is the same
  250 MB, but it is now a declared number that can be stated and changed without editing code.

## 3. What "enforced" does and does not mean under concurrency

A ceiling that compares against a **count** is checked before the write and not held under a lock.
Two requests arriving in the same instant can both pass the check and leave a meeting one over the
limit. That overshoot is bounded by concurrency and is accepted deliberately: the alternative is
serializing every chat message and every join behind a lock, which costs far more than the last seat.

Where an invariant has to be exact, the **database** enforces it, not the count:

- the partial unique index from `EnforceSingleActiveMeetingRecording` is what actually prevents two
  concurrent recordings **of one meeting**;
- the unique index on `(MeetingId, AuthorParticipantId, ClientMessageId)` is what actually prevents
  a duplicate chat message.

That second one was a real fault found while writing these tests. Duplicate suppression on chat
reads before it writes, so it only ever caught a retry that arrived *after* the first send had
committed. A client retrying on a slow connection has both requests in flight at once: both read
nothing, both write, and the index refuses one — which surfaced to that caller as a failure while
their message was sitting in the room. The refused send now re-reads and reports the message that
landed. `MeetingChat_UnderSimultaneousRetriesOfOneMessage_StoresItOnceAndReportsItToEveryCaller`
fires six simultaneous retries at a real database and fails if that is reverted.

## 4. Rate limits (separate from capacity)

Capacity bounds what a meeting holds; rate limits bound how fast anyone may ask. The guest surface
used to share **one 12/minute per-IP budget across everything a guest does**, so an office behind
one NAT competed for it and a chat poll spent the same allowance as an OTP request
([threat model](MEETINGS-THREAT-MODEL.md) A-08). It is three budgets now:

| Policy | Limit | Partition | Applies to |
|---|---|---|---|
| `meeting-guest-verify` | 12/min | remote address | inspect, request-code, verify-code — everything callable without a session |
| `meeting-guest` | 180/min | guest session token (SHA-256, first 16 hex chars), else address | session reads, chat/note polling, join tokens, moderation |
| `meeting-guest-upload` | 10/min | as above | guest file uploads |
| `meeting-webhook` | 600/min | remote address | the anonymous provider webhook |
| `meeting-collaboration-write` / `-upload` | 60/min, 10/min | user id | member chat, notes, uploads |

The partition key is a hash, never the raw session token: partition keys sit in memory and can reach
a log or a dump, and the raw token is a bearer credential for the meeting.

## 5. Retention of guest access records

Guest sessions and OTP challenges are *access* records, not meeting content, so meeting retention
never reached them and nothing deleted them — the tables grew for the life of the deployment
(threat model A-07). `MeetingRetentionCleanupService` now purges **spent** rows older than
`GuestAccessRecordRetentionDays`: a session that has expired or been revoked, a challenge that has
expired or been consumed. They are deleted outright rather than soft-deleted, because a soft delete
would leave exactly the growth this fixes.

Guest **decisions** are deliberately untouched: they are the moderation audit trail — who admitted,
denied, removed or revoked whom.

## 6. What this document does not claim

Nothing here is a load test. These are the limits the code enforces, not measured throughput:

- No production or staging run has held 50 participants in one room. The declared participant
  number is a policy ceiling, and the media path that would have to serve it is still only proven by
  the 2026-09-04 two-device call.
- `MaxConcurrentRecordings` is set to 1 because no Egress capacity run has happened yet
  (Phase 6's open external item). Raise it only with a measured worker.
- The per-organization live-meeting ceiling has not been exercised against real media load.

Those measurements belong to P7.6 (infrastructure) and the Phase 6 staging certification.
