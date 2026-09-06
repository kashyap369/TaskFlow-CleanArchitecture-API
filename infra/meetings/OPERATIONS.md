# TaskFlow Meetings — Backup, secret rotation and incident procedure

> **Phase 7 / P7.7.** The operational procedures Meetings needs that are not triage: what has to be
> backed up and what a restore actually gives you back, how to rotate each secret and what breaks
> while you do, and how to run a meetings incident.
>
> For "a meeting is broken right now", start at [RUNBOOK.md](RUNBOOK.md) instead. For what the data
> *is* and how long it is kept, see [docs/MEETINGS-PRIVACY.md](../../docs/MEETINGS-PRIVACY.md).
>
> **Status: written, not yet exercised.** Every procedure below is derived from the code and the
> deployment as it stands on 2026-09-06. None of the restore or rotation drills has been performed
> against production. Until each is run once and this line is amended, treat the timings as
> estimates and the steps as untested.

---

## 1. What a meeting actually lives in

Three stores, and a meeting is only whole when all three agree.

| Store | Holds | Lost if not backed up |
|---|---|---|
| **PostgreSQL** (`TaskFlowDb`) | every meeting row: schedule, participants, access links, guest sessions, attendance, chat, notes, consent, recording metadata, webhook receipts | the meeting itself. Recording *files* would survive in storage but nothing would know they exist, because the storage key lives only in the database |
| **Object storage** (`ObjectStorage:Bucket`, S3-compatible; local disk in development) | shared files and completed recordings under `meetings/{meetingId}/…` | the artefacts. The database would keep rows pointing at objects that are gone, and playback would fail with the file missing |
| **Configuration and secrets** (Dokploy service environment, plus the optional `appsettings.Production.json` File Mount) | `LiveKit__*`, `Meetings__*`, `ObjectStorage__*`, `JwtSettings__SecretKey`, SMTP credentials, the database connection string | not user data, but without it the API cannot issue join tokens, verify webhooks, read storage or authenticate anyone. RUNBOOK §A is the story of exactly this |

**LiveKit itself holds nothing that needs backing up.** Rooms, participants and tracks are ephemeral
by design; the durable record is TaskFlow's. LiveKit's own configuration is in
[`livekit.yaml`](livekit.yaml) / [`egress.yaml`](egress.yaml) in this repository, and Redis (where
present) is coordination state, not a system of record.

## 2. Backup

**PostgreSQL.** A logical dump is sufficient and is the format a restore is easiest to reason about:

```bash
pg_dump --format=custom --no-owner --file=taskflow-$(date +%Y%m%d-%H%M).dump "$DATABASE_URL"
```

**Object storage.** Mirror the bucket. With the MinIO client:

```bash
mc mirror --overwrite --remove taskflow/meetings-bucket /backups/objects
```

Note `--remove`: without it a mirror keeps objects that retention has deliberately erased, and the
backup silently becomes the reason "deleted" is not true. With it, the mirror follows deletions —
which is what [docs/MEETINGS-PRIVACY.md](../../docs/MEETINGS-PRIVACY.md) §3 assumes when it says the
bytes are gone. Decide this deliberately; do not leave it to a default.

**Configuration.** Export the service environment and keep it in the secret manager, not in git. The
File Mount described in [RUNBOOK.md](RUNBOOK.md) §2 is the mechanism that survives a redeploy; its
content is itself a secret.

**Ordering matters.** Take the database dump **after** the storage mirror. That way every object
referenced by the database exists in the backup. The reverse order can produce a dump whose rows
point at objects the mirror never saw — a restore that fails on playback rather than obviously.

**What is not backed up anywhere today:** nothing schedules any of the above. There is no backup job,
no retention policy for backups, and no monitoring that a backup succeeded. Establishing that is
outstanding, and no RPO or RTO can honestly be quoted until it exists.

## 3. Restore, and what it does to meetings

Restoring TaskFlow restores meetings, but four things are specific to this feature and none of them
are obvious:

1. **A restore can resurrect revoked access.** Access-link revocations and guest-session kills are
   database state. Restoring a snapshot taken *before* a revocation brings the leaked link and its
   guest sessions back to life, with their original expiry. **After any restore, re-apply every
   revocation made after the snapshot** — if the reason for the restore was a leak, this is the whole
   point, not a footnote.
2. **In-flight recordings will hang.** `MeetingRecordingRecoveryService` reconciles active recordings
   against the provider every minute, but a restored `ProviderEgressId` refers to an Egress job the
   running LiveKit has never heard of; the status lookup returns nothing and the recording stays in
   `Starting`/`Recording`/`Processing` indefinitely. Find them and fail them explicitly:

   ```sql
   SELECT "Id", "MeetingId", "Status", "ProviderEgressId", "StorageKey"
   FROM "MeetingRecordings"
   WHERE "Status" IN (2, 3, 4) AND "IsDeleted" = FALSE;   -- Starting, Recording, Processing
   ```

   Check whether each `StorageKey` has a playable object. If it does, the file can be kept and the row
   marked ready; if it does not, mark it failed so the host is told the truth rather than waiting.
3. **Meetings stuck at `Live` do not become live again.** The database says `Live`; LiveKit has no such
   room. Participants will be issued tokens into an empty room. End those meetings so they archive
   properly and the retention clock gets an `ActualEndUtc` — without one, retention never sweeps them
   ([docs/MEETINGS-PRIVACY.md](../../docs/MEETINGS-PRIVACY.md) §8.3).
4. **Webhook receipts are the deduplication ledger.** Restoring an older set means provider events
   already applied can be applied again. The handlers are idempotent, so this is safe, but attendance
   intervals may be re-opened; expect noise, not corruption.

**Verify a restore against meetings specifically**, not just "the app starts": open `/admin/settings`
and confirm **Meetings readiness** is Ready, list a past meeting and open its archive, play one
recording end to end, and create + start + end a throwaway meeting.

## 4. Secret rotation

Every secret below is rotatable without a migration. What differs is the blast radius, and it is not
intuitive — read the row before you rotate.

| Secret | Rotating it breaks | Notes |
|---|---|---|
| `LiveKit__ApiSecret` (and `ApiKey`) | **every issued join token, immediately**, and every webhook signature until LiveKit is updated too | Rotate **both sides together**. Anyone mid-call keeps their existing connection but cannot rejoin. Minimum 32 characters. Verify with the readiness panel's live signing proof, not by joining |
| `JwtSettings__SecretKey` | every access and refresh token — **all users are signed out** — **and every pending guest OTP challenge** | The coupling is easy to miss: `OneTimeCodeSettings:SecretKey` falls back to `JwtSettings:SecretKey` when unset, and production leaves it unset. Rotating the JWT key silently invalidates codes people are typing in right now. Set `OneTimeCodeSettings__SecretKey` explicitly first if you want the two independent |
| `OneTimeCodeSettings__SecretKey` | pending guest OTP challenges only | Guests simply request a new code. The cheapest rotation here |
| `ObjectStorage__SecretKey` / `AccessKey` | file and recording reads **and** retention deletes | A wrong value makes retention fail quietly — it logs a warning and skips, so data stays past its window. Verify by opening one archived file after rotating |
| SMTP `EmailSettings__Password` | invitations and guest codes stop sending | Guests cannot enter at all; members are unaffected |
| Database password | everything | Ordinary application rotation, not meeting-specific |

**What rotation does *not* invalidate:** access-link tokens and guest-session tokens are stored as
plain SHA-256 hashes with no server-side key, so no rotation revokes them. The only lever for a
leaked link is revoke or rotate the link itself — which now also kills the sessions issued from it
and ejects those guests (threat model M-01).

**Procedure.** Change it in the Dokploy service environment **and** in the File Mount if one exists —
environment wins where both are present, so a stale mount is harmless but a stale environment silently
overrides a correct mount. Redeploy. Then read `/admin/settings` → Meetings readiness before
announcing that anything is fixed; it reports what the running process loaded, which is the exact
failure the panel was built for.

## 5. Incident procedure

**Severity, for meetings specifically:**

| | Situation |
|---|---|
| **Sev 1** | Someone was recorded without consent; a recording or shared file was exposed to someone unauthorized; guest access reached a meeting it was never granted |
| **Sev 2** | No one can join any meeting; recordings are failing across the deployment; retention has not run for days (data kept past its declared window) |
| **Sev 3** | One organization or one meeting affected; a recording stuck in `Processing`; degraded media quality |

**Containment levers, strongest first.** All are configuration or ordinary API calls; none needs a
deploy of new code:

1. `Meetings__RecordingEnabled=false` — stops all recording immediately.
2. `Meetings__GuestsEnabled=false` — cuts off every guest path deployment-wide, leaving members working.
3. `Meetings__Enabled=false` — takes the whole feature down.
4. Per-meeting: revoke the access links (kills sessions and ejects guests), remove participants, end
   the meeting.

Flags 1–3 are the staged-rollout switches in reverse, which is why the rollout is staged in that
order in the first place.

**Investigation.** Meeting telemetry deliberately carries no meeting content, so metrics tell you
*that* something failed and the trace or log line tells you *which meeting* —
[docs/MEETINGS-OBSERVABILITY.md](../../docs/MEETINGS-OBSERVABILITY.md) §1 and §5. `/admin/meetings/health`
evaluates the alert rules in-process, and reports its own limits: the window is per instance, and it
will not claim a clean hour it has not observed.

**Evidence to preserve before touching anything:** the `MeetingRecordingConsents` rows for the
recording in question (per-participant decision and timestamp — this is the record that answers a
consent complaint), `MeetingGuestDecisions` (who admitted whom), `MeetingAttendance` (who was
connected and when), and the access-link row with its revocation timestamp. Note that a retention
sweep will remove consent and attendance with the rest of the meeting when its window closes, so
export them rather than assuming they will still be there.

**Notification.** A Sev 1 involving a recording or an exposed file is a personal-data incident: who
must be told, and within what window, depends on the data-residency and jurisdiction decisions that
are still open ([docs/MEETINGS.md](../../docs/MEETINGS.md) §12, item 6). Until those are made, an
incident of this kind must be escalated to the owner rather than handled inside the on-call rotation.

## 6. Related documents

- [RUNBOOK.md](RUNBOOK.md) — production triage; the four faults of 2026-09-04 and how each was proved.
- [RECORDING.md](RECORDING.md) — Egress deployment, object storage setup, and the pre-enablement gate.
- [README.md](README.md) — pinned component versions and the local media environment.
- [docs/MEETINGS-PRIVACY.md](../../docs/MEETINGS-PRIVACY.md) — what is stored, for how long, and what deletion does.
- [docs/MEETINGS-OBSERVABILITY.md](../../docs/MEETINGS-OBSERVABILITY.md) — signals, alert rules, per-rule runbooks.
