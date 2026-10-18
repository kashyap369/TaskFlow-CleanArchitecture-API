# TaskFlow — Attendance

> **Canonical reference for the attendance module** (API side). The mobile client, Taskaro
> (`kashyap369/Taskaro-app`), consumes it; its `docs/ATTENDANCE.md` holds the longer design and
> the later phases (policies, zones), and its `docs/FIELD-TRACKING.md` the field-tracking plan.
> Since 2026-10-18 the Angular client has an Attendance section too (§8).
>
> **Status: implemented 2026-10-18, deployed straight to `main` at the developer's instruction,
> without a local build or test run** (the authoring environment has no .NET SDK). See §6.

## 1. What it does

Members punch in and out of their organization. Each punch **must carry a selfie**. A member may
punch in and out several times a day. A manager — whoever holds `ViewAttendance` — sees who is
in, out, on leave or off for a holiday; whoever holds `ManageAttendance` corrects records and sets
the organization's time zone.

## 2. Rules

| Rule | Where it is enforced |
|---|---|
| Punch time is the **server's clock**; the request carries none | `PunchInCommand` / `PunchOutCommand` |
| **Selfie required** on both punches; JPEG / PNG / HEIC by **file signature**, ≤ 5 MB | controller (missing file) + `AttendancePhotoPolicy` |
| **One open session per member per organization** | filtered unique index `IX_AttendanceSessions_OrganizationId_UserId` (`"PunchOutAtUtc" IS NULL AND "IsDeleted" = FALSE`); a racing duplicate becomes `409 ATTENDANCE_ALREADY_PUNCHED_IN` in `UnitOfWork` |
| Owner or active member only | `IOrganizationAccessGuard.EnsureOrganizationAsync` |
| Work date = calendar day in the org's time zone (default UTC) | `AttendanceClock` + `AttendanceSettings.TimeZone` |
| Forgotten punch-out: closed at punch-in + `AutoCloseAfterHours` (default 16) as **AutoClosed**, which counts **0** minutes worked | `AttendanceMaintenanceService` every 30 min, and inline at the next punch-in so nobody is locked out |
| Adjustments need `ManageAttendance` and a reason; the **first** originals are kept | `AttendanceSession.Adjust` |
| Selfies deleted after `PhotoRetentionDays` (default 90); the record stays | `AttendanceMaintenanceService` |
| Photos are private: served only through the API, to the member or a `ViewAttendance` holder | `GetAttendancePhotoQuery` |
| Leave and holidays come from `CalendarEntries` via `GetCalendarEntriesQuery` | `AttendanceReads.LoadCalendarAsync` |

Days with no punch, leave or holiday are **`NoRecord`**, not "absent": there are no work schedules
yet, so a weekend looks identical.

## 3. Storage

Selfies go to object storage (`IObjectStorage`; RustFS / any S3-compatible server in production)
under `attendance/{orgId}/{userId}/{guid}-{in|out}`. The row stores the key, content type, size and
SHA-256 — the `PlannerAsset` pattern. The upload happens first; if the database save then fails,
the object is deleted, so storage does not collect orphans.

## 4. Endpoints — `api/attendance`

| Method | Route | Who | Returns |
|---|---|---|---|
| `POST` | `organization/{orgId}/punch-in` | member | `multipart/form-data`: `photo` (required), `note` → session id |
| `POST` | `organization/{orgId}/punch-out` | member | same form → 204 |
| `GET` | `organization/{orgId}/me/today` | member | `AttendanceTodayDto` |
| `GET` | `organization/{orgId}/me?from&to` | member | `AttendanceDayDto[]`, newest first, ≤ 62 days |
| `GET` | `organization/{orgId}/member/{userId}?from&to` | self, or `ViewAttendance` | same |
| `GET` | `organization/{orgId}/roster?date&teamId` | `ViewAttendance` | `AttendanceRosterDto` |
| `GET` | `organization/{orgId}/settings` | member | `AttendanceSettingsDto` |
| `PUT` | `organization/{orgId}/settings` | `ManageAttendance` | `{ timeZone, autoCloseAfterHours, photoRetentionDays }` → 204 |
| `PUT` | `{sessionId}/adjust` | `ManageAttendance` | `{ punchInAtUtc, punchOutAtUtc, reason }` → 204 |
| `GET` | `{sessionId}/photo/{in\|out}` | self, or `ViewAttendance` | the image |

Error codes: `ATTENDANCE_SELFIE_REQUIRED`, `ATTENDANCE_SELFIE_TOO_LARGE`,
`ATTENDANCE_SELFIE_TYPE_UNSUPPORTED`, `ATTENDANCE_ALREADY_PUNCHED_IN` (409),
`ATTENDANCE_NOT_PUNCHED_IN` (409), `ATTENDANCE_SESSION_NOT_FOUND`, `ATTENDANCE_PHOTO_NOT_FOUND`.

All responses are bare payloads (no `ApiResponse` envelope), enums as numbers, dates as UTC with
`DateOnly` work dates as `yyyy-MM-dd`.

## 5. Schema — migration `20261018120000_AddAttendance`

**Purely additive**: tables `AttendanceSessions` and `AttendanceSettings`, four indexes. No existing
table, column or row is touched. New permissions `ViewAttendance` and `ManageAttendance` are rows,
inserted by `OrganizationPermissionSeeder` at startup — no migration.

## 6. ⚠️ How this was shipped, and what to check

The snapshot and the migration's `Designer.cs` were **written by hand**, because the authoring
environment cannot run `dotnet ef`. EF Core 10 refuses to start (`PendingModelChangesWarning`) if the
snapshot does not match the model exactly, and this API migrates at startup — so a mismatch would
stop the whole API booting. Mitigations taken: the new blocks were modelled on `CalendarEntry`
(same column kinds, no foreign keys); the Designer is byte-identical to the previous one except the
header and the two new blocks; and every one of the 37 columns was machine-checked across entity,
configuration, snapshot and migration (name, CLR type, column type, length, nullability).

**Confirm on the next local session:**

```
dotnet build
dotnet ef migrations has-pending-model-changes --project TaskFlow.Infra --startup-project TaskFlow.Api
dotnet test
```

**If production fails to start after this deploy:** redeploy the previous image. Nothing here
modifies existing data, so rolling the code back is safe; the two new tables are simply unused.

## 7. Not in this version

Work schedules and late marking, per-role / per-member hours, approved zones and out-of-zone
reasons, team-lead routing, correction requests — designed in Taskaro's `docs/ATTENDANCE.md` §6a,
planned as the next attendance phase. Face matching / liveness: not planned.

## 8. Field tracking — route of tracked members while punched in (2026-10-18, branch only)

> **Written on `claude/wonderful-darwin-8lt5ko`, not built, not merged.** Verify exactly as §6:
> `dotnet build`, `dotnet ef migrations has-pending-model-changes`, `dotnet test`.

### Rules

| Rule | Where |
|---|---|
| Off until `ManageAttendance` turns it on and picks **tracked roles** (role ids, never names) | `UpdateFieldTrackingSettingsCommand`, `AttendanceTrackedRoles` |
| Seeing anyone's route or position needs **`ViewFieldTracking`** (new; owner bypasses). Seeing who is in does **not** imply it | read handlers |
| A member may always see **their own** route | `GetFieldTrackingDayQuery` |
| Whether a session is tracked is decided **once, at punch-in** (member's role tracked + tracking on) | `FieldTrackingRules.MustTrackAsync` → `AttendanceSession.IsTracked` |
| A tracked member must have **consented** (current policy version) to punch in | `PunchInCommandHandler` → `TRACKING_CONSENT_REQUIRED` |
| Points only for the member's **own tracked** session, open — or closed ≤ 48 h (offline phones) | `RecordLocationPointsCommandHandler` |
| A point must fall inside punch-in − 1 min … punch-out (or now) + 1 min; duplicates by time skipped | same; unique index `(AttendanceSessionId, RecordedAtUtc)` |
| Point times are the **phone's** (recorded offline); `ReceivedAtUtc` is stored too. Punch times stay server-side | `LocationPoint` |
| Mock (fake-GPS) fixes are **flagged, not refused** | `LocationPoint.IsMock` |
| Raw points **hard-deleted** after `LocationRetentionDays` (7–365, default 30) — a deliberate exception to soft delete; segments, stops, km kept | `FieldTrackingProcessingService.PurgeExpiredPointsAsync` |
| Gaps (no fix ≥ 5 min, ≥ 200 m apart) become **Inferred** segments; clients must draw them as guesses | `FieldTrackingRoutes` |

### Endpoints — `api/attendance/organization/{orgId}/tracking`

| Method | Route | Who | Returns |
|---|---|---|---|
| `GET` | `me/status` | member | `MyFieldTrackingStatusDto` (tracked? consented? open session tracked? intervals) |
| `POST` | `consent` | member | `{ policyVersion }` → 204 (`TRACKING_POLICY_OUTDATED` if not current) |
| `DELETE` | `consent` | member | 204 — withdraws |
| `POST` | `points` | member | `{ attendanceSessionId?, points: [{ recordedAt, latitude, longitude, accuracyMeters?, speedMetersPerSecond?, headingDegrees?, isMock, batteryPercent?, isCharging?, isBatterySaverOn? }] }` (≤ 1000) → `{ attendanceSessionId, accepted, skipped, trackingActive }` |
| `GET` | `me?date=` | member | `FieldTrackingDayDto` for one work date (default today) |
| `GET` | `member/{userId}?date=` | self or `ViewFieldTracking` | same |
| `GET` | `live` | `ViewFieldTracking` | tracked open sessions with the newest fix (or the punch-in position) |
| `GET` | `settings` | `ManageAttendance` or `ViewFieldTracking` | enabled, retention, policy version, `roadMatchingAvailable`, roles with `isTracked` + member count |
| `PUT` | `settings` | `ManageAttendance` | `{ trackingEnabled, locationRetentionDays, trackedRoleIds[] }` → 204 |

`punch-in` / `punch-out` also accept optional form fields `latitude`, `longitude`, `accuracyMeters`
(`.` decimal separator).

Error codes: `TRACKING_CONSENT_REQUIRED`, `TRACKING_NOT_REQUIRED` (409, session not tracked),
`ATTENDANCE_NOT_PUNCHED_IN` (409), `TRACKING_SESSION_CLOSED` (409, > 48 h), `TRACKING_BATCH_OVERLAP`
(409, retry), `TRACKING_POLICY_OUTDATED`, `TRACKING_ROLE_NOT_FOUND`. The app stops recording on the
409s except the overlap.

### Processing and Valhalla

`FieldTrackingProcessingService` (every 3 min, 50 sessions a pass) rebuilds a session's route from
**all** its points whenever it has unprocessed ones: drop fixes worse than 50 m or faster than
70 m/s → find stops (≥ 10 min within 100 m) → collapse stops, split at gaps → match each recorded
piece (≤ 1000 points) with Valhalla `trace_route`, route each gap with `route`. Segments and stops
are replaced and exactly the points used are marked processed, in one transaction.

`Valhalla:BaseUrl` (env `Valhalla__BaseUrl`, e.g. `http://valhalla:8002`) — **empty means not
configured**, and the cleaned track is stored unsnapped (`isSnapped = false`); nothing fails. Valhalla
down → points stay unprocessed and are retried. Also `Valhalla:Costing` (`auto` default; `motorcycle`,
`motor_scooter`, `bicycle`, `pedestrian`), `TimeoutSeconds` (30), `SearchRadiusMeters` (50). Server
setup: Taskaro `tools/valhalla/README.md`.

### Schema — migration `20261018180000_AddFieldTracking`

Additive only. Columns: `AttendanceSessions` + `IsTracked` (default false), `PunchIn/OutLatitude`,
`…Longitude`, `…AccuracyMeters` (nullable); `AttendanceSettings` + `TrackingEnabled` (default false),
`LocationRetentionDays` (default 30). Tables: `AttendanceTrackedRoles`, `FieldTrackingConsents`
(both soft-deletable, filtered unique indexes), `LocationPoints`, `TrackedRouteSegments`,
`TrackStops` (plain `BaseEntity`, hard-deleted). Snapshot and Designer hand-written; every column
machine-checked across entity, snapshot and migration, as in §6.

Tests: `FieldTrackingRoutesTests` (clean, stops, gap bridging, chunking, polyline) and
`FieldTrackingTests` (domain). Expected values were checked with a Python port of the algorithm;
the C# has not been run.
