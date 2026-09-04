# Meetings production runbook

**What this is.** Production meetings went from "nothing works" to a real two-device call on
2026-09-04. Four separate faults were in the way, none of them obvious, and two of them sent the
investigation in the wrong direction for hours. This file records what was actually wrong, how each
was proved, and how to check the same things quickly next time.

Read this **before** theorising about firewalls, TURN or NAT. Three of the four faults looked like
networking and none of them were.

---

## 0. Sixty-second triage

Work top to bottom. Each step rules out a whole class of cause.

| # | Check | Healthy answer |
|---|---|---|
| 1 | `/admin/settings` -> **Meetings readiness** (AdminOnly) | Status **Ready**, join-token signing proven |
| 2 | `curl https://livekit.inksphere.space/` | `OK` |
| 3 | `POST /api/meeting/{id}/join-token` as an assigned participant | `200` with a token |
| 4 | LiveKit logs: does a session appear at all? | `starting RTC session` |
| 5 | LiveKit logs: `participant closing` — read **`reason`** and **`sessionDuration`** | Session lasts as long as the user stayed |

Step 1 exists because of fault A below. It answers "did this *process* get its configuration?",
which is not the same question as "is it configured in Dokploy".

---

## 1. The four faults, and how each was proved

### A. Dokploy did not inject the service environment into the container

**Symptom.** Every join refused with `400 DOMAIN_RULE_VIOLATION - "LiveKit media is not enabled."`,
while the Dokploy **api -> Environment** tab plainly showed all eleven `LiveKit__*` / `Meetings__*`
variables, and `https://livekit.inksphere.space` was healthy.

**Proof.** Dokploy -> api -> **Open Terminal** -> `env | grep -i livekit` -> **no output**. The
variables were saved but never reached the running container. A normal redeploy did *not* fix it.

**Fix applied.** On the host:

```bash
docker service update --env-add LiveKit__Enabled=true \
  --env-add "LiveKit__Url=wss://livekit.inksphere.space" \
  --env-add "LiveKit__ApiKey=..." --env-add "LiveKit__ApiSecret=..." taskflow-api-kf0oee
```

**Version note.** This was on Dokploy `v0.29.14`. The platform was upgraded to `v0.30.5` on
2026-09-05 and the configuration survived the upgrade. Whether the injection bug itself is fixed is
**not yet proven** - see section 2, and verify at the next API deploy rather than assuming.

**Trap.** If the secret is wrong or shorter than 32 characters the API refuses to start, Swarm rolls
the service back automatically, and you see `rollback: update rolled back due to failure`. That is
`ValidateOnStart` doing its job - the API keeps serving on the old spec, so the attempt is safe.
Re-run with correct values.

### B. A speaker preference tore down working calls (Android only)

**Symptom.** Joins succeeded, the roster and self-view appeared, then the user was returned to
pre-join after ~3 seconds. On every network - 5G, Wi-Fi, broadband. Desktop Chrome was fine.

**Proof.** LiveKit logged `mediaTrack published` for audio *and* video over `"connectionType": "udp"`
with a ~290ms connect, then `participant closing ... reason: "CLIENT_REQUEST_LEAVE",
sessionDuration: "3.09s"`. Media was never the problem.

**Cause.** `MeetingRoomService.connect()` applied device preferences inside its `try`. Android Chrome
does not implement `setSinkId`, so `switchActiveDevice('audiooutput', ...)` threw, the `catch`
disconnected the room, and the user bounced. Desktop Chrome implements `setSinkId`, which is exactly
why the same build worked there.

**Fix.** Join preferences now run through `applyPreference()` - failures become a device warning and
the call continues. Only the connection itself may abort a join.

### C. `room_finished` archived meetings nobody attended

**Symptom.** Test meetings kept vanishing from **Live now** into **Past**, forcing a new meeting for
every attempt.

**Cause.** LiveKit closes a room when the last participant leaves - including after fault B ejected
everyone - and the webhook ended the meeting unconditionally.

**Fix.** Auto-end now requires one attendance interval of at least
`Meetings:AutoEndMinimumSessionSeconds` (default 30). Attendance is still always closed.

### D. Recording could never have worked

Production ran only Redis and LiveKit - **no Egress worker**, and LiveKit cannot record itself. The
service is now defined in `dokploy.compose.yml`; see [RECORDING.md](RECORDING.md) for the enablement
order and the still-open legal/consent gate.

---

## 2. Surviving a redeploy

Anything applied with `docker service update --env-add` lives **outside** Dokploy. A Dokploy deploy
rewrites the service spec from its own configuration and can drop those values, and the failure is
silent until someone cannot join.

> **STATUS (2026-09-05): NOT YET CONFIGURED — planned for the next session.** Production currently
> relies on the manual `docker service update` values, so a redeploy can still drop them. Until the
> File Mount exists, the post-deploy checklist below is mandatory, not a formality.

**Preferred: a Dokploy File Mount** (api -> **Advanced -> Volumes -> Add Volume -> File Mount**). It
is stored in Dokploy, applied on every deploy, and keeps secrets out of git:

- Mount path `/app/appsettings.Production.json` - confirm the app's `WORKDIR` with `pwd` in the
  container terminal before trusting `/app`.
- The content must carry **both** sections, because the mount replaces the file baked into the image:

```json
{
  "Meetings": {
    "Enabled": true,
    "GuestsEnabled": true,
    "RecordingEnabled": false,
    "GuestSessionMinutes": 60,
    "DefaultRetentionDays": 90,
    "RecordingConsentTimeoutSeconds": 60,
    "AutoEndMinimumSessionSeconds": 30
  },
  "LiveKit": {
    "Enabled": true,
    "Url": "wss://livekit.inksphere.space",
    "ApiKey": "REPLACE_ME",
    "ApiSecret": "REPLACE_ME",
    "WebhookToleranceSeconds": 301
  }
}
```

Environment variables still take precedence over this file where both are present, so a working
Dokploy env simply overrides it. The mount is a floor, not a conflict.

### After every API deploy — the whole checklist

**1. Check readiness.** Sign in as platform admin, open `/admin/settings`, read the **Meetings
readiness** panel.

- **Ready** -> done, nothing else to do.
- **Disabled** with a "not propagated" blocker -> the deploy dropped the config. Go to step 2.

No admin login to hand? Open any live meeting and press Join. `LiveKit media is not enabled` is the
same failure.

**2. Only if it says Disabled** — re-apply on the host. The leading space keeps the secret out of
shell history (with the default `HISTCONTROL=ignorespace`):

```bash
 docker service update --env-add LiveKit__Enabled=true    --env-add "LiveKit__Url=wss://livekit.inksphere.space"    --env-add "LiveKit__ApiKey=YOUR_KEY"    --env-add "LiveKit__ApiSecret=YOUR_SECRET" taskflow-api-kf0oee
```

Success is `verify: Service ... converged` **without** a `rollback:` line. Then re-check readiness.

A `rollback:` line means the API refused to start — almost always a wrong or too-short secret (it
must be at least 32 characters). Swarm keeps the previous spec serving, so a failed attempt is safe;
just re-run with the right values. Copy them from Dokploy: api -> Environment -> the eye icon.

**3. Stop needing step 2.** Configure the File Mount above, or confirm Dokploy `v0.30.5` injects the
environment correctly — after the first deploy on it, if readiness stays **Ready**, the original bug
is fixed and this checklist becomes a formality.

---

## 3. Reading LiveKit logs without drowning

Dokploy -> **meetings-media -> Logs -> container `...-livekit-1`**. Individual entries are enormous
(a join logs the entire SDP), so filter to the lifecycle lines: `starting RTC session`,
`participant active`, `participant closing`, `room closed`.

Two fields decide almost everything:

- **`reason`** on `participant closing`. `CLIENT_REQUEST_LEAVE` means **our own code** called
  `room.disconnect()` - it is indistinguishable from the user pressing Leave, so never read it as a
  user action. Any other reason points at the network or the server.
- **`sessionDuration`**. A constant duration across different devices and networks is a client bug,
  not a network fault. Networks fail unevenly; code fails on a timer.

`publisherCandidates` and `connectionType` on `participant active` show whether media actually
established. If you see `mediaTrack published`, stop investigating the network.

The client's own teardown reason is also recorded in `sessionStorage` under
`taskflow.meeting.lastDisconnect` and rendered on pre-join as *"Previous attempt ended by ..."*. The
trigger names the exact branch: `page-destroyed`, `user-left`, `connect-reset`, `connect-failed`.

---

## 4. What proved to be a red herring

Recorded so the same hours are not spent twice:

- **TURN / TLS.** Diagnosed as the cause of the mobile failure. It was not. TURN is configured
  UDP-only with no `domain`, which is genuinely incomplete, but media established over plain UDP
  throughout - the phone's failure was fault B.
- **The firewall.** `ufw` allows only 22/80/443, which looks alarming. It is irrelevant: Docker
  publishes ports through its own chain and bypasses ufw's INPUT rules. `7881/tcp`, `7882/udp` and
  `3478/udp` were listening and reachable the whole time.
- **IPv6 / carrier NAT.** Plausible while the failure looked mobile-specific; disproved the moment
  desktop showed the identical 3-second drop.

The lesson worth keeping: a failure that reproduces identically across three different networks is
not a network failure.
