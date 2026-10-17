# TaskFlow — Attendance

> **Canonical reference for the attendance module** (API side). The mobile client, Taskaro
> (`kashyap369/Taskaro-app`), consumes it; its `docs/ATTENDANCE.md` holds the longer design and
> the later phases (policies, zones, field tracking). The Angular client does not use it yet.
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
