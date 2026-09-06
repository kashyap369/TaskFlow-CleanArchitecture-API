# TaskFlow Meetings — Privacy, retention and deletion

> **Phase 7 / P7.7, revised by P7.8.** What a meeting stores, how long each piece survives, what "deleted" actually
> does to it, and where meeting data leaves TaskFlow. Read this before answering a data-subject
> request, before changing `Meetings:DefaultRetentionDays` or the cleanup service, and before
> telling anyone — a customer, a reviewer, the privacy policy — how long a meeting is kept.
>
> This document describes the system **as built**, not as intended. Where the behaviour is weaker
> than the claim a privacy policy would like to make, §8 says so plainly instead of rounding up.
> The public-facing wording this document supports lives in the frontend repository at
> `src/app/features/public/legal-page/legal-documents.ts`.

---

## 1. What one meeting stores

Everything below is per-meeting unless stated. "Personal data" means it identifies a person on its
own, not merely by joining to another table.

| Store | What it holds | Personal data | Removed by retention? |
|---|---|---|---|
| `Meetings` | title, description, schedule, timezone, settings, opaque room name, `RetentionDays` | title/description are free text and may contain anything a person typed | **No — the row survives indefinitely** |
| `MeetingParticipants` | one row per invited person: `UserId` **or** `NormalizedEmail`, `DisplayName`, access level, badge, admission state | **Yes — a guest's email address and chosen display name** | Yes — the row survives, but the email and display name are cleared (§2) |
| `MeetingBadgeDefinitions` | label, colour, icon | no | No |
| `MeetingAccessLinks` | SHA-256 `TokenHash`, mode, `LockedEmail`, use count, expiry, revocation | **Yes — the invited address on a private invitation** | Yes — `LockedEmail` is cleared and the link is revoked (§2) |
| `MeetingGuestChallenges` | email, HMAC-SHA256 code hash, attempts, expiry | **Yes — email** | Yes — separate 30-day sweep once spent |
| `MeetingGuestSessions` | SHA-256 session-token hash, participant, originating link, expiry, revocation | by reference only | Yes — separate 30-day sweep once spent |
| `MeetingGuestDecisions` | who admitted or denied which guest, and when | by reference only | **No — deliberately: it is the moderation audit trail** |
| `MeetingAttendance` | provider connection id, join and leave times | by reference only | Yes |
| `MeetingMessages` | chat body, author, reply target, client message id | **Yes — free text written by people** | Yes (soft delete — see §3) |
| `MeetingNotes`, `MeetingNoteRevisions` | current note body and its revision history | **Yes — free text** | Yes (soft delete) |
| `MeetingAssets` | file metadata plus a storage key under `meetings/{meetingId}/…` | file names are free text | Yes — **and the object is really erased** |
| `MeetingRecordings` | status, Egress id, storage key `meetings/{id}/recordings/…`, size, duration, failure reason | the video itself | Yes — **and the object is really erased**, whatever the status |
| `MeetingRecordingConsents` | per-participant accepted / declined / timed-out and when | by reference only | Yes (soft delete) |
| `MeetingWebhookReceipts` | provider event id, event type, timestamp | no | **No** |
| Object storage | shared files and completed recordings, private, no public URL | the file and video contents | Yes — a real `DeleteAsync` |

Nothing in the meeting tables stores an IP address. IP addresses appear only where the rate limiter
keys a bucket in memory, and in whatever the reverse proxy logs.

## 2. The retention clock

`Meetings.RetentionDays` is per meeting, set at creation, defaulting to `Meetings:DefaultRetentionDays`
(90). It is chosen by the organizer, so two meetings in the same organization can differ.

- **The clock starts at** `ActualEndUtc ?? ScheduledEndUtc ?? CreatedAt` — see
  `MeetingCollaboration.RetainUntil`.
- **Two things happen at expiry, and they are not the same thing.**
  1. `MeetingCollaborationAccess.EnsureRetained` refuses every collaboration and archive request for
     an `Ended` meeting past its window with `MEETING_ARCHIVE_EXPIRED`. This is immediate and exact:
     it is evaluated per request against the clock, so an expired archive becomes unreachable the
     moment the window passes, whether or not any sweep has run.
  2. `MeetingRetentionCleanupService` — a hosted service on a **6-hour** timer — actually deletes.
- **The sweep considers every meeting, in whatever state it stopped.** Since P7.8 the sweep's clock
  is `ActualEndUtc ?? ScheduledEndUtc ?? UpdatedAt ?? CreatedAt` — the first three terms are the same
  ones `RetainUntil` uses, and `UpdatedAt` is the extra term the never-ended states need, so editing
  a long-lived draft restarts its window rather than leaving it eligible from the day it was created.
  A cancelled meeting, an abandoned draft and a meeting wedged in `Live` are therefore all swept.
  **The one exception:** a `Live` meeting with an open attendance interval is skipped, because
  somebody is still on the call however old the row looks.
- **Storage first, database second.** For each expired meeting the sweep deletes every asset object
  and every recording object — since P7.8 that is *every* recording, not only the `Ready` ones, so a
  partial artefact written by a failed Egress goes too. Rows are only cleared if every object delete
  succeeded. An object that is already missing counts as success; any other storage failure skips
  that meeting entirely, so the next pass retries it. The consequence to know: a meeting whose
  object storage is unreachable keeps its rows — the pass logs an error naming how many meetings it
  could not finish, but nothing alerts on it.
- **Personal data on the roster and the invitations is cleared, not deleted.** Since P7.8 the sweep
  also nulls `NormalizedEmail` and `DisplayName` on every `MeetingParticipants` row and `LockedEmail`
  on every `MeetingAccessLinks` row, and revokes each of those links. The rows themselves stay,
  because attendance, messages, consents and the guest moderation trail all reference them and would
  otherwise be orphaned — but they stop naming a person. Reads already fall back to the linked user's
  name or `Participant`, so a redacted guest renders as an unnamed participant.
- **Guest access records are on their own clock.** Spent guest sessions and OTP challenges — expired,
  revoked or consumed — are hard-deleted once older than `Meetings:GuestAccessRecordRetentionDays`
  (30), independent of any meeting's retention. See [MEETINGS-CAPACITY.md](MEETINGS-CAPACITY.md) §5.

## 3. What "deleted" does, precisely

This is the part most likely to be overstated in a policy document.

**Objects are erased.** Shared files and completed recordings are removed from object storage by a
real delete — at retention, at host-initiated recording delete, and at asset delete. After that the
bytes are gone from TaskFlow; what a backup snapshot still holds is
[infra/meetings/OPERATIONS.md](../infra/meetings/OPERATIONS.md) §2.

**Database rows are soft-deleted.** `AuditableEntity.SoftDelete()` sets `IsDeleted` and `DeletedAt`.
It nulls out no column. A global EF query filter hides the row and every Dapper query filters
`IsDeleted = FALSE`, so the content is unreachable through all API routes — but **the chat body, the
note text and the file name are still bytes in PostgreSQL** after retention has run.

Personal columns are the exception, and they are the reason a soft delete was not enough: since
P7.8 retention **nulls** the guest email address, the guest display name and a private invitation's
locked address rather than flagging their rows (§2). Those bytes really are gone from PostgreSQL.

So the accurate claim is: *after the retention window, meeting content is permanently inaccessible
through TaskFlow; shared files and recordings are erased from storage; the email addresses and
display names identifying guests and invitees are erased from the database; and residual content
records are retained in a non-readable state.* The inaccurate claim is "we delete your meeting data
after N days".

## 4. Guests

A guest is not a TaskFlow account. What TaskFlow learns about one, and keeps:

- **Their email address**, given to request a code, stored normalized (upper-cased) on
  `MeetingParticipants` and on the challenge. The challenge goes after 30 days; the participant row
  stays but its address and display name are cleared when the meeting's retention window closes
  (§2). Between those two points the address is held.
- **A display name** they type at the lobby, which becomes their name in the room and on every chat
  message. Any printable text up to 120 characters (threat model A-06).
- **No password, no profile, no account.** The session is an opaque random token; TaskFlow stores
  only its SHA-256 hash, so the database cannot be used to resume anyone's session.
- **Their access is revocable, and the revocation reaches them.** Revoking or rotating the link kills
  every guest session issued from it and ejects those guests from the live room (P7.2 / M-01).
- **Archive access continues after the meeting** for the full retention window — messages, note,
  files, and recordings where enabled. That is open owner decision 7 in [MEETINGS.md](MEETINGS.md)
  §12 and threat-model residual A-04, not a considered privacy position.

## 5. Recording

- **Recording is off unless the deployment turns it on.** `Meetings:RecordingEnabled` is `false` in
  production today; with it off, every recording route answers `MEETING_RECORDING_DISABLED`.
- **Only a host may request one**, and only for a `Live` meeting.
- **Consent is collected from the people actually in the room**, read from the provider's live
  roster. If the roster cannot be read the request is refused outright rather than falling back to a
  smaller set (P7.2 / M-02): recording fails closed.
- **Every required participant must accept.** One decline, or the consent window
  (`Meetings:RecordingConsentTimeoutSeconds`, 60) elapsing, fails the recording, and the provider is
  never asked to start. A late joiner is gated at the join token and must accept to enter.
- **The evidence is per participant and immutable**: accepted / declined / timed out plus the instant
  of the decision, on `MeetingRecordingConsents`. A decision cannot be re-decided; `Decide` ignores a
  second call.
- **The recording is announced while it runs** — all clients show recording state, and the room
  carries a persistent indicator.
- **The consent record dies with the recording.** Retention soft-deletes consents alongside the
  recording they justify, so the audit trail does not outlive the artefact.
- **Storage is private.** Recordings are written to a private object path and served only through
  `GET /meeting/{id}/recordings/{recordingId}/content` after an authorization check. There is no
  public URL and no presigned link.

## 6. Where meeting data leaves TaskFlow

| Recipient | What reaches it | Notes |
|---|---|---|
| **LiveKit** (self-hosted) | audio, video and screen tracks; the opaque room name; an opaque per-join participant identity (`m{meetingId}-p{participantId}-{guid}`); the participant's **display name**; metadata carrying participant id, access level and badge label | Self-hosted on infrastructure the deployment controls, so not a third-party disclosure — but it is a second system holding names and live media. LiveKit does not persist tracks. |
| **LiveKit Egress** | the composed room, written as MP4 into TaskFlow's own object storage | Only while a recording runs. |
| **Object storage** (S3-compatible, or local disk in development) | shared files, recordings | Private bucket; TaskFlow proxies every read. |
| **SMTP provider** | guest email addresses, meeting title, time, host name, the invite link and the OTP | The one genuine external recipient in the default deployment. |

One detail worth knowing before it surprises someone: a **member with no display name joins under the
local part of their email address** (`user.Email.Split('@')[0]` in `MeetingRoomAccess`), so that
string is what LiveKit receives and what every other participant — guests included — sees.

Meeting **telemetry deliberately carries none of this**: no email, token, room name, title, display
name, chat text, file name or IP may become a metric tag or log field, and a test enforces it. See
[MEETINGS-OBSERVABILITY.md](MEETINGS-OBSERVABILITY.md) §1.

## 7. Handling a request about meeting data

**Access / export — a member.** Everything they can see is already behind an authenticated route: the
meeting detail, `GET /meeting/{id}/archive`, messages, note, assets, recordings. There is no bulk
export endpoint; assemble from those routes.

**Access / export — a guest.** They hold a scoped session for the one meeting they were invited to
and can read the same archive through the guest routes until the retention window closes. Beyond
that, what TaskFlow holds about them is their address and display name on `MeetingParticipants` plus
their admission decision — reachable only by an operator with database access, and only until
retention clears the address and display name.

**Erasure — a member.** Removing them from a meeting changes participant state; it does not erase the
chat they wrote. Content erasure is per artefact: the host can delete assets and recordings, and
retention removes the rest on schedule.

**Erasure — a guest, on request.** This is the case the product does not yet serve, and it should be
answered honestly rather than improvised:

1. Revoke the access links they used (`DELETE /meeting/{id}/access-links/{linkId}`), which also kills
   their sessions and ejects them from any live room.
2. Their spent sessions and challenges disappear within 30 days on their own.
3. **Their email address and display name on `MeetingParticipants` are cleared when the meeting's
   retention window closes**, along with the address any private invitation was locked to (§2).
   Before that point no route removes them, so an erasure request that cannot wait for the window
   still needs a manual, audited database update by an operator.

**Legal hold.** There is no hold mechanism. The only lever that keeps the sweep away from a meeting is
raising that meeting's `RetentionDays`, which is not exposed after creation — so a hold is currently a
database change.

## 8. What this document does not claim

Stated here so nobody has to discover them during an audit.

1. **Meeting titles and descriptions survive retention.** They are free text and may contain
   anything a person typed, and the `Meetings` row is never deleted or redacted — the archive is
   made unreachable rather than removed. Guest email addresses, guest display names and the address
   a private invitation was locked to are no longer in this list: P7.8 made the sweep clear them.
2. **Soft-deleted content remains in PostgreSQL.** §3. This is the reason the redaction in §2 nulls
   the columns rather than relying on a soft delete: a flag would have left the addresses in place.
3. **Retention stalls quietly when object storage is unreachable**, because a failed delete skips the
   meeting. The pass logs an error naming how many meetings it could not finish, but no alert rule
   watches it, so nothing pages anyone — and since P7.8 what is held past its window includes the
   personal data the sweep would otherwise have cleared.
4. **No data-subject export, erasure or legal-hold tooling exists.** §7 is a manual procedure.
5. **The retention default (90 days) and the guest archive window are not owner-approved.** They are
   the conservative defaults of [MEETINGS.md](MEETINGS.md) §12, items 2 and 7.
6. **Data residency is undecided** (§12 item 6), and no jurisdiction-specific recording-consent review
   has been recorded. That review is a Phase 6 exit criterion and remains open.

## 9. Related documents

- [MEETINGS-THREAT-MODEL.md](MEETINGS-THREAT-MODEL.md) — the security review and its accepted residuals.
- [MEETINGS-CAPACITY.md](MEETINGS-CAPACITY.md) — declared limits, rate limits, guest-record retention.
- [MEETINGS-OBSERVABILITY.md](MEETINGS-OBSERVABILITY.md) — the telemetry privacy contract.
- [MEETINGS-SUPPORT.md](MEETINGS-SUPPORT.md) — answering a user who cannot join, share or record.
- [infra/meetings/OPERATIONS.md](../infra/meetings/OPERATIONS.md) — backup/restore, secret rotation, incident procedure.
