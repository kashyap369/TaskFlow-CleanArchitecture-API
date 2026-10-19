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
| `GET` | `organization/{orgId}/attended-members?date` | `ViewAttendance` **or** `ViewFieldTracking` | `AttendedMembersDto` — only the members with a session on that work date |
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

> **On `claude/wonderful-darwin-8lt5ko`.** Verified locally 2026-10-18: build ok, `dotnet test`
> 141/141, `has-pending-model-changes` → no changes. Not yet merged or deployed.

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
| `GET` | `me/points?date=` | member | `FieldTrackingPointsDto` — the day's stored fixes, **sampled** (§9) |
| `GET` | `member/{userId}/points?date=` | self or `ViewFieldTracking` | same |
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


## 9. The attendance map's three reads (2026-10-19)

Three things the developer asked for after the first field shifts. All three are **reads only** — no
migration, no new column, nothing about how a route is built.

### a. Who actually punched in — `attended-members`

The web's member picker on the map tabs listed every member of the organization, so most choices
could only draw an empty map. `GetAttendedMembersQuery` returns one row per member with at least one
`AttendanceSessions` row on the chosen work date: name, role, session count, first punch-in, last
punch-out, still-in, and whether any of that day's sessions was tracked.

Deliberate choices:

- **A new route, not a flag on `roster`.** The roster needs `ViewAttendance`, returns a full status
  row per member and folds in the calendar; the map tabs are gated on `ViewFieldTracking`, and
  someone holding only that would have been refused their own picker.
- **Either permission is enough** (`ViewAttendance` **or** `ViewFieldTracking`) — it reveals no more
  than *this person was at work that day*.
- **No email and no contact detail** in the row, so granting `ViewFieldTracking` does not quietly
  widen into the directory. The one place an address does appear is the member's own day read, where
  the caller is already authorized to see that person in full.
- Work date, not a timestamp range — the same filter the route read uses, so the two always agree.

### b. What a punch pin shows — `PunchDetailDto`

`TrackedSessionDto` now carries `punchIn` and `punchOut` (`punchOut` is null while the session is
open). Each holds the kind, the instant, the coordinates and their accuracy, the punch note,
`hasPhoto`, and the battery.

- **The selfie is not in the payload.** `GET api/attendance/{sessionId}/photo/{in|out}` stays
  authenticated; `hasPhoto` tells the client whether to fetch it with the session's token and show
  it from an object URL. Nothing was loosened to put a face on a map pin.
- **Battery is the nearest recorded fix within ten minutes of the punch**, and null when there is
  none. A punch records no battery of its own, and a reading from an hour later would be worse than
  a blank. `GetFieldTrackingDayQueryHandler.PunchDetail` is public so this is unit-tested.
- **There is no address.** No column stores one and no geocoder is called — a reverse-geocoding
  service would also mean every tracked coordinate leaving the server, which is the opposite of why
  Valhalla is self-hosted. The clients say "not recorded" rather than leaving a blank row.
- `FieldTrackingDayDto` also gained `email`, so a map pin can name the member it belongs to with
  something more stable than a display name. **There is no employee code in the schema** — see
  "Not built" below.

### c. The fixes behind a route — `…/points`

`LocationPoints` already stores every field the hover needs, so this is a read and a tooltip.
`FieldTrackingPointsDto` returns, per session, the total number of fixes and a **sample** of them
with `recordedAtUtc`, coordinates, accuracy, speed, heading, battery, charging, battery-saver and
the mock flag.

**Sampling — and why.** A shift at one fix every five seconds is thousands of rows (a real eight-hour
day is ~5 800), and the client is drawing hover targets, not a dataset.
`GetFieldTrackingPointsQueryHandler.Sample` keeps one fix in every stride of `ceil(count / 400)`,
always including the first and the last, so the sample starts and ends where the session does.

- **An even stride by position, not by time.** Fixes arrive at a near-fixed interval, so the two are
  the same in practice — but picking by time can thin a dense stretch to nothing or leave a sparse
  one untouched, and an index stride cannot.
- **400 per session** is about one fix a minute across eight hours, which is the resolution a person
  reading a map can use. The route line is drawn from the matched segments and loses nothing to this.
- The response carries `totalPointCount`, `sampledPointCount` and `maxPointsPerSession` so the page
  can say *showing 400 of 5 812* — a thinned set must never read as the whole record.
- Thinning is in C#, not SQL, so it is one implementation with unit tests over it. The query itself
  is a plain indexed read on `(AttendanceSessionId, RecordedAtUtc)`.
- Permission is the same as the route: your own fixes always, anyone else's only with
  `ViewFieldTracking`.

### Not built

- **No employee code.** The detail panel was asked to show one and nothing in the schema holds it —
  not `Users`, not `OrganizationMembers`. The clients show the member's email and user id instead.
  Adding a real code is a column on `OrganizationMember` plus a way to set it on the members page,
  which is a change to member management rather than to the map.
- **No address for a punch or a fix**, for the reason in (b).

### Checks

Build clean. `dotnet test` **155/155** — 141 pre-existing, 9 unit tests over `PunchDetail` and
`Sample`, and 5 integration tests in `AttendanceMapReadApiIntegrationTests` that drive all three
reads over HTTP against a freshly migrated database. The integration tests are the ones that matter
here: these are Dapper reads, so a typo in a quoted identifier is invisible to the compiler and to
every unit test. They also pin the refusals — an outsider is refused `attended-members`, and a
member without `ViewFieldTracking` can read their own fixes but not a colleague's.

> **Local test note.** `TaskFlow.Api/appsettings.json` is gitignored, and the integration fixture
> reads the connection string from it. In a fresh worktree those 19 integration tests fail with
> `FileNotFoundException` until that file is copied in from a checkout that has it.
