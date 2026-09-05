# Meetings — threat model and abuse review (Phase 7 / P7.2)

**Reviewed 2026-09-05** against the code in `D:\Projects\TMS\TaskFlow` (backend) and
`D:\Projects\TMS\TaskFlowUI\TaskFlowApp` (Angular). Companion documents:
[MEETINGS.md](MEETINGS.md) (roadmap), [ARCHITECTURE.md](ARCHITECTURE.md) (the three authorization
layers), [../infra/meetings/RUNBOOK.md](../infra/meetings/RUNBOOK.md) (production triage).

This is a review of what the code actually does, not of what the design intends. Every row marked
**FIXED** was a live defect in `main` before this session; every row marked **ACCEPTED** is a real
residual risk that a reader should be able to argue with.

---

## 1. What is being protected

| Asset | Where it lives | Worst case if it leaks |
|---|---|---|
| Live audio/video/screen share | LiveKit room, never TaskFlow | An uninvited party watches a private conversation |
| Join credentials | Minted per request, 10-minute TTL, never stored | Same, for the token's lifetime |
| Chat, shared note, note revisions | PostgreSQL | Meeting content disclosed after the fact |
| Uploaded files | Private object storage, keyed `meetings/{id}/{guid}{ext}` | Document disclosure; malware distribution |
| Recordings | Private object storage, MP4 | The most sensitive asset in the feature |
| Attendance | PostgreSQL | Who met whom, and for how long |
| Guest email addresses | `MeetingParticipants.NormalizedEmail` | Contact disclosure; targeting |
| LiveKit API key/secret | Server configuration only | Full control of every room on the server |

## 2. Trust boundaries

1. **Browser → API.** Nothing from the browser is authority. Identity comes from the JWT
   (`ICurrentUserService`) or from a guest session token hash; never from a request body.
2. **API → LiveKit.** The API holds the key/secret and mints least-privilege tokens
   (`RoomJoin` only, plus publish/moderate as the meeting's own rules allow;
   `RoomCreate`/`RoomList`/`RoomRecord`/`IngressAdmin` are always false).
3. **LiveKit → API (webhooks).** Anonymous by transport, authenticated only by the signature over
   the raw body, then deduplicated by provider event id.
4. **Guest → organization.** A guest is not a TaskFlow account. A guest session grants exactly one
   meeting and nothing else in the organization.

## 3. Actors

| Actor | How they are established | Ceiling |
|---|---|---|
| Organization member with `ManageMeetings` | JWT + `IOrganizationPermissionChecker` | Manage any meeting in that organization |
| Meeting creator (host) | `Meetings.CreatedByUserId` | Manage their own meeting; cannot be demoted or revoked |
| Assigned member | `MeetingParticipants.UserId` | Join, collaborate at their access level |
| Verified guest | Access link → emailed 6-digit code → tab-scoped session token | One meeting, at the link's default access level |
| Anonymous internet | — | Link inspection and code request only, rate-limited |
| LiveKit | Signed webhook | Attendance and Egress state transitions only |

## 4. Findings

### 4.1 Fixed in this review

| # | Surface | Finding | Fix |
|---|---|---|---|
| M-01 | Access links | **Revoking a leaked link did not evict anyone who had already used it.** `RevokeAccessLink` marked the link revoked, which stops new verifications, but sessions already exchanged for that link stayed valid for the rest of `Meetings:GuestSessionMinutes` (default 60) — room, chat, files and archive included. Revocation is the organizer's only lever when a link leaks, and it did not reach the people the leak let in. | `MeetingGuestSession` now records `AccessLinkId` (migration `AddMeetingGuestSessionAccessLink`, additive and nullable). Revoke and rotate revoke every active session issued from that link and eject those participants from the live room. Re-verification then fails because the link itself is gone. |
| M-02 | Recording consent | **Consent could be collected from an empty room.** The required-consent set was built from open attendance intervals, which are written by provider webhooks. A delayed or lost webhook left the set as the host alone, so consent completed instantly and everybody already connected was recorded without ever seeing a prompt. The join-token consent gate does not help — it only blocks *new* joins. | `IMeetingMediaProvider.ListRoomParticipantIdentitiesAsync` reads the provider's live roster, and the consent set is roster ∪ open attendance ∪ requester. If the roster cannot be read the request is refused with `MEETING_RECORDING_ROSTER_UNAVAILABLE` — recording fails closed. |
| M-03 | Recording consent | **Any assigned participant could veto any recording**, including one who never joined the call, by posting a decline: the entity added a consent row on demand and a decline failed the recording. | A decline is now accepted only from a participant who was actually asked (`MeetingRecording.WasConsentRequestedFrom`). A late joiner may still *accept*, which is what the join gate needs. |
| M-04 | Guest sessions | A guest who had been revoked, denied or removed could still rename themselves; `ConfirmGuestDisplayName` was the one guest path with no participant-state check. | All guest-session entry points share `MeetingGuestAccessRules.EnsureStillAllowed`. |
| M-05 | Chat | `ReplyToMessageId` came from the client and was never checked, so a message could be anchored to a thread in a meeting the author cannot see. | The reply target must exist in the same meeting. |
| M-06 | Guest sessions | A session pointing at a soft-deleted participant hit `Enumerable.Single` and produced an unhandled 500 instead of an authorization failure. | Same shared guard; returns `MEETING_GUEST_SESSION_INVALID`. |
| M-07 | Webhooks | The provider webhook endpoint is necessarily anonymous, and had no rate limit — forged bodies cost a body read plus an HMAC each, free to send and not free to reject. | `meeting-webhook` fixed-window policy, 600/minute per source address, far above LiveKit's real event rate. |
| M-08 | Access links | Rotating a link whose expiry had already passed minted a replacement that was dead on arrival. | Rotation keeps the later of the old expiry and seven days out. |

Regression coverage: `TaskFlow.Tests/Application/MeetingSecurityHardeningTests.cs`, one test per finding.

### 4.2 Verified sound — no change needed

- **Join tokens.** Server-derived only. Meeting-management authority alone does not grant a token;
  the caller must be an assigned participant (`GetMeetingJoinTokenCommandHandler`). Guests must be
  `Admitted` by an organizer. Grants are least-privilege, TTL 10 minutes, capped at 15 by the
  adapter. The browser never sees a room name or a provider secret.
- **Participant identities.** `m{meetingId}-p{participantId}-{32 hex}`, minted server-side. Moderation
  validates both the prefix and the full pattern before calling the provider, so a moderator cannot
  aim a mute at an identity outside the meeting.
- **Moderation.** Host/co-host only, must themselves be `Admitted`; a host can never be targeted, and
  a co-host cannot target another co-host.
- **Privilege escalation via participants and links.** `AddRegisteredParticipant`, `UpdateParticipant`
  and `AddAccessLink` all refuse `Host`; the host cannot be demoted or revoked; badge ids are checked
  against the meeting that owns them, so no cross-meeting badge assignment.
- **OTP.** 6 digits, HMAC-SHA256 keyed server-side and bound to `(accessLinkId, normalizedEmail)`, so
  a code is useless on another link. 10-minute expiry, 5 attempts then burnt, 60-second resend floor,
  fixed-time comparison, single-use. Codes are never stored in the clear.
- **Link and session tokens.** 256 bits of CSPRNG, URL-safe, stored only as SHA-256 hashes. The raw
  value is returned exactly once, at creation.
- **Webhook replay.** Signature over the raw body, then `MeetingWebhookReceipt` keyed by provider
  event id makes reprocessing a no-op. Unknown room or unknown egress id is dropped silently.
- **Uploads.** Content-type allowlist (PDF/PNG/JPEG/TXT/DOCX) cross-checked against the extension
  *and* against magic bytes; `Path.GetFileName` defeats traversal; the stream is counted while it is
  read, so a lying `Content-Length` cannot exceed the cap; per-meeting quota of 10 × the file cap;
  storage keys are server-generated GUIDs, never the uploaded name. Downloads are refused until the
  scanner returns Clean, and are served with `X-Content-Type-Options: nosniff`.
- **SQL.** Every meeting read is a parameterised Dapper query with quoted identifiers and an
  `IsDeleted` filter. No string interpolation of user input into SQL anywhere in the feature.
- **Meeting enumeration by list.** `GetOrganizationMeetingsQuery` is `IOrganizationScopedRequest`, so
  `AccessGuardBehavior` enforces organization membership, and the SQL itself still filters to
  manage-permission, creator, or non-denied participant.
- **Feature flags.** `Meetings:Enabled`, `:GuestsEnabled` and `:RecordingEnabled` are enforced by
  action filters on every route and answer `404`, not `403`, so a disabled feature is not advertised.
- **Development probe.** `/api/dev/meetings/livekit/*` is mapped only in Development and additionally
  refuses any non-loopback caller.
- **Readiness endpoint.** AdminOnly; discloses the API key as an 8-character SHA-256 fingerprint and
  the secret as a length only.

### 4.3 Accepted residual risks

| # | Risk | Why it is accepted | Revisit when |
|---|---|---|---|
| A-01 | `GET /api/meeting/{id}` answers `404` for a meeting that does not exist and `403` for one the caller cannot see, so an authenticated user can test whether an id exists in another organization. | Discloses existence of an integer id and nothing else — no title, organization or participant. Collapsing the two costs a clear error for the common in-organization mistake. | If meeting ids ever become externally meaningful. |
| A-02 | Holding a private-invitation link, an attacker can learn whether a given email is the invited one, because the locked-email check reports a distinct error. | The lobby already shows a masked hint of the locked address by design, so the oracle is granted by the product, not by this check. | If the masked hint is removed. |
| A-03 | The invitation email puts the organizer's **email address** in the `HostName` slot, where the rest of the product uses a display name. | The recipient was deliberately invited by that person. | Cheap to change; do it with the next email-template pass. |
| A-04 | A guest keeps archive access — messages, note, files, and recordings if enabled — for the full `RetentionDays` after the meeting ends. | Open decision 7 in [MEETINGS.md](MEETINGS.md#12-decisions-that-require-explicit-approval-before-implementation). Conservative default until the owner decides. | Owner decision. |
| A-05 | A `Viewer` can download a ready recording. | Same open decision; the alternative (host-only playback) is a product call, not a security default. | Owner decision. |
| A-06 | Guest display names accept any printable text up to 120 characters, unlike badge labels which reject `< > &`. | Angular escapes interpolated text, the name reaches LiveKit only as a JWT claim, and it never enters an email template. | If a display name is ever rendered as HTML or embedded in an email. |
| A-07 | Expired `MeetingGuestSessions` and `MeetingGuestChallenges` rows are never purged; retention cleanup covers content, not access records. | Growth is bounded by guest volume and the rows are small; the decision records are deliberately kept as an audit trail. | Track under P7.3 capacity. |
| A-08 | The whole guest controller shares one 12/minute per-IP budget, so guests behind one NAT compete, and a chat poll competes with a file upload. | Availability, not security, and no production guest traffic exists yet to size it against. | P7.3, with real numbers. |
| A-09 | A revoked guest's *current* media connection survives until the eject call lands, and if that call fails it survives until the room ends. | Their TaskFlow session is dead immediately, so they cannot rejoin or reach any stored meeting data; the failure is logged. | If LiveKit gains a synchronous revocation. |

## 5. Attacker walkthroughs

**A stranger with a leaked reusable link.** They can inspect the meeting's title, time and host name,
and request a code — but the code goes to *their* mailbox, and reaching the room still needs the
organizer to admit them (`MEETING_GUEST_NOT_ADMITTED`). If the link is capped by `MaximumUses` or
locked to one email, they are refused earlier. Once the organizer revokes the link, they are ejected
and cannot verify again (M-01).

**An organization member who is not on the meeting.** They can see the meeting in the list only if
they hold `ManageMeetings`. They cannot get a join token, read chat, read the note, list or download
files, read the archive, or moderate — every one of those paths resolves a participant row for their
user id first and refuses without it.

**A malicious guest already admitted.** They are capped at the link's default access level, which can
never be `Host`. They cannot moderate unless the organizer made them a co-host. They cannot veto a
recording they were not asked about (M-03), cannot post files outside the allowlist, cannot exceed the
meeting quota, and cannot reply into another meeting's thread (M-05). The organizer removing them
revokes their sessions and ejects them.

**Someone forging webhooks.** Without the API secret the signature check fails and the request is
`401` before any handler runs, now under a rate limit (M-07). Replaying a captured, validly signed
event is a no-op after the first delivery.

## 6. What this review did not cover

These are the remaining Phase 7 packages, not gaps in this one:

- Capacity and concurrency limits under declared ceilings — **P7.3**.
- Structured metrics, traces and alerting on the abuse paths named here — **P7.4**.
- End-to-end coverage of the full create → invite → OTP → join → collaborate → record → archive
  journey and its denial paths — **P7.5**.
- TURN reachability from restrictive networks, production network topology, and secret rotation —
  **P7.6**.
- Privacy policy, retention and jurisdiction review for recording — **P7.7** and open decisions 2, 6
  and 7.

No penetration test has been run against a deployed environment. Every statement above is derived
from reading the code and from the regression tests listed in §4.1.
