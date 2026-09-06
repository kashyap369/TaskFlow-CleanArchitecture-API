# TaskFlow Meetings — Support troubleshooting

> **Phase 7 / P7.7.** For whoever answers "I can't get into the meeting". It maps what a person
> *says* to what the API actually refused, tells you which of them can fix it — the user, the
> organizer, or an operator — and says when to stop and escalate.
>
> This is the **user-facing** half. When the answer is "the platform is broken", hand off to
> [infra/meetings/RUNBOOK.md](../infra/meetings/RUNBOOK.md) (production triage) and
> [MEETINGS-OBSERVABILITY.md](MEETINGS-OBSERVABILITY.md) §5 (per-alert runbooks). Nothing here
> requires database access.

---

## 1. Three questions first

Almost every meeting report resolves against these before any investigation:

1. **Who is reporting — a member of the organization, or a guest with a link?** They travel entirely
   different code paths and different error codes. A guest never sees a "sign in" problem, and a
   member never sees an OTP problem.
2. **Is the meeting `Live`?** Nothing about the room works before the host presses Start. The room,
   chat, notes and file upload are all writable only while the meeting is `Live`; before that a
   participant sees the lobby and after it an archive.
3. **Does it affect everyone, or one person?** One person is almost always access state, browser or
   network. Everyone at once is a platform fault — go to the RUNBOOK.

**Where to look, in this order:** the exact error text the user sees (every message below is what the
API returned, not a generic client string) → `/admin/settings` → **Meetings readiness** (is the
feature even enabled and configured on the deployed API?) → **Meetings health** on the same page (are
the alert rules firing?) → the RUNBOOK.

## 2. The feature looks missing entirely

| What they see | What it is | Who fixes it |
|---|---|---|
| No **Meetings** item in the organization sidebar | `Meetings:Enabled` is `false` on this deployment, or the user's role lacks the meetings permissions | Operator (flag) or organization owner (role) |
| `MEETINGS_NOT_AVAILABLE` from any meeting route | The whole meetings feature is switched off | Operator — this is a deliberate flag, not a fault |
| `MEETING_GUESTS_NOT_AVAILABLE` on a guest link | `Meetings:GuestsEnabled` is `false` deployment-wide | Operator |
| `MEETING_GUESTS_DISABLED` on one meeting's link | Guests are enabled on the platform but **not on that meeting** | The organizer, in the meeting's settings |
| Recording controls absent, or `MEETING_RECORDING_DISABLED` | `Meetings:RecordingEnabled` is `false`. It is `false` in production today, on purpose | Operator, and only after the Phase 6 legal/product sign-off |

Confirm any of these in one place: `/admin/settings` → **Meetings readiness** shows the flags the
**running process** actually loaded, not what someone typed into a deployment screen. That
distinction is the whole point of the panel — see RUNBOOK §A.

## 3. A member cannot join

| Error | What actually happened | Resolution |
|---|---|---|
| `MEETING_NOT_FOUND` | No meeting with that id, or it belongs to another organization | Check the link is for the right workspace |
| `MEETING_ACCESS_DENIED` | They are signed in and the meeting exists, but they are not assigned to it | The organizer adds them as a participant |
| `MEETING_ROOM_NOT_LIVE` | The host has not started it, or it has ended | Wait for the host. This is not a fault |
| `MEETING_NOT_JOINABLE` | The meeting is cancelled or in a state that cannot be joined | Organizer re-creates or re-schedules |
| `MEETING_ROOM_ACCESS_REVOKED` | Their participation was revoked, denied or removed | Organizer re-admits them if that was a mistake |
| `MEETING_PARTICIPANT_LIMIT_REACHED` | The meeting is full at `MaxParticipantsPerMeeting` (50) | Someone leaves, or the organizer splits the meeting. See [MEETINGS-CAPACITY.md](MEETINGS-CAPACITY.md) |
| `MEETING_CONCURRENT_LIMIT_REACHED` | The organization already has `MaxConcurrentLiveMeetingsPerOrganization` (10) meetings live | End one |
| `MEETING_ARCHIVE_EXPIRED` | Past its retention window; the content is gone | Nothing recovers this. See [MEETINGS-PRIVACY.md](MEETINGS-PRIVACY.md) §2 |
| **"LiveKit media is not enabled"** or a join that fails with no meeting error at all | The API cannot issue a join token — media configuration is missing or did not reach the running process | **Operator.** This is the 2026-09-02 failure mode: read the readiness panel, then RUNBOOK §A |

**They joined but see or hear nobody.** That is not an access problem. Confirm both people show in
the participant list: if they do, it is media (RUNBOOK §B and §3); if they do not, they are in
different meetings or one never actually connected.

## 4. A guest cannot get in

The guest path is: open link → inspect → enter email → receive a 6-digit code → verify → wait to be
admitted → join. Find the step that failed.

| Error | What actually happened | Resolution |
|---|---|---|
| `MEETING_ACCESS_INVALID` | The link is not recognised, the meeting behind it is gone, or the email does not match a link locked to one address | Ask the organizer to re-send the invitation. Note this message is deliberately vague on the "request a code" step so a stranger cannot test addresses |
| `MEETING_LINK_UNAVAILABLE` | The link is expired, revoked, or has hit its use limit | The organizer issues a new link. A rotated link invalidates the old one by design |
| `MEETING_CODE_INVALID` | Wrong code, expired code, or too many attempts | Request a new code. Codes are single-use |
| **The code email never arrives** | Check spam first. Then check whether *other* TaskFlow email works — if nothing sends, it is SMTP, not meetings | Operator if all mail is failing |
| `MEETING_ACCOUNT_REQUIRED` | The invitation is bound to a TaskFlow account and the person is not signed in | Sign in, then reopen the link |
| `MEETING_ACCOUNT_EMAIL_MISMATCH` | They are signed in as a different account than the one invited | Sign out and use the invitation email, or ask for an invitation to the account they actually use |
| `MEETING_GUEST_NOT_ADMITTED` | Verified, but the host has not admitted them from the lobby | Only the host can clear this. Tell them to expect a prompt |
| `MEETING_GUEST_SESSION_INVALID` | Their session expired (`Meetings:GuestSessionMinutes`, 60 by default) or the browser lost it | Verify the email again — this is normal after a long wait |
| `MEETING_GUEST_ACCESS_REVOKED` | The host ended this guest's access | Deliberate. Do not work around it; the organizer decides |

**A guest was in and got dropped.** Almost always a revoked or rotated link: revocation now ejects
active guests on purpose (threat model M-01). Ask the organizer whether they revoked a link.

## 5. In the meeting

| Error | Meaning | Resolution |
|---|---|---|
| `MEETING_COLLABORATION_READ_ONLY` | Chat, notes and uploads are writable only while the meeting is `Live` | Expected before start and after end |
| `MEETING_CHAT_DENIED` | A viewer trying to chat in a meeting where viewers may not | The organizer can allow viewer chat in meeting settings |
| `MEETING_NOTE_EDIT_DENIED` | Note editing is limited to hosts/co-hosts for this meeting | Organizer setting |
| `MEETING_NOTE_CONFLICT` | Two people saved the note from stale versions | Reload and re-apply. Notes are conflict-aware, not multi-cursor — this is by design |
| `MEETING_MESSAGE_LIMIT_REACHED` | 5 000 messages in one meeting | Capacity, not a fault |
| `MEETING_FILE_TOO_LARGE` | Over 25 MB | Split or compress |
| `MEETING_FILE_TYPE_DENIED` / `MEETING_FILE_SIGNATURE_INVALID` | Not in the allowlist (PDF, PNG, JPEG, TXT, DOCX), or the file's contents do not match its claimed type | Convert it. The signature check is deliberate — a renamed executable is refused |
| `MEETING_FILE_COUNT_LIMIT_REACHED` / `MEETING_FILE_QUOTA_EXCEEDED` | 100 files, or 250 MB total for the meeting | Delete something, or use a project file |
| `MEETING_MODERATION_DENIED` / `MEETING_MODERATION_TARGET_DENIED` | Not a host/co-host, or trying to mute or remove someone they may not | Expected. Hosts cannot be removed by co-hosts |

## 6. Recording

Recording is **off in production**. If it is enabled in the environment being supported:

| Error | Meaning | Resolution |
|---|---|---|
| `MEETING_RECORDING_DISABLED` | The deployment flag is off | Operator |
| `MEETING_RECORDING_DENIED` | Not the host. Only a host may request a recording | Ask the host |
| `MEETING_RECORDING_CONSENT_REQUIRED` | Someone in the room has not answered the consent prompt yet | Wait, or the request times out after 60 seconds and fails |
| `MEETING_RECORDING_ROSTER_UNAVAILABLE` | The API could not read who is in the room, so it refused to record rather than record people it could not ask | **Platform issue.** Media stack — RUNBOOK. Do not retry blindly |
| `MEETING_RECORDING_CAPACITY_REACHED` | The deployment already has its one simultaneous recording running | Wait for the other to finish. This is a deployment-wide ceiling |
| `MEETING_RECORDING_ACTIVE` | Trying to delete a recording that is still running | Stop it first |
| `MEETING_RECORDING_NOT_READY` | Egress is still processing | Wait. If it stays here, escalate — see `recording_failures` in [MEETINGS-OBSERVABILITY.md](MEETINGS-OBSERVABILITY.md) §5 |
| Recording ends as **Failed** | Consent declined or timed out, or Egress failed | The failure reason is on the recording. A declined consent is a correct outcome, not a bug |

**"The recording disappeared."** Check the retention window before assuming a fault: recordings are
erased from storage with the rest of the meeting when it expires, and that is irreversible.

**"Was I recorded without knowing?"** No path allows it: consent is collected from the live roster,
every required participant must accept, a late joiner is gated at the join token, and the indicator
runs for the whole recording. If someone believes otherwise, escalate it as a security report — the
per-participant consent record with timestamps is the evidence, and it is kept as long as the
recording is.

## 7. When to escalate

Escalate to an operator, with the exact error code and the time:

- Any error naming media, LiveKit, or a join token that the readiness panel does not explain.
- `MEETING_RECORDING_ROSTER_UNAVAILABLE`, or recordings stuck in `Processing`.
- Everyone in an organization failing at the same step at the same time.
- A participant reporting they were recorded without a prompt.
- Anything the readiness panel reports as **not ready** on a deployment where meetings are supposed
  to work.

Hand them: [infra/meetings/RUNBOOK.md](../infra/meetings/RUNBOOK.md) §0 for sixty-second triage,
[MEETINGS-OBSERVABILITY.md](MEETINGS-OBSERVABILITY.md) §5 for the alert that matches, and
[infra/meetings/OPERATIONS.md](../infra/meetings/OPERATIONS.md) if it turns out to be an incident,
a leaked link or a lost artefact.
