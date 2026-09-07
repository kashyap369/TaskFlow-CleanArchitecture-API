# TaskFlow Meetings — production provisioning and staged rollout

> **Phase 7 / P7.6.** The last package before Meetings is a released feature: complete the production
> LiveKit/Redis/TURN topology, prove it works from a network that blocks UDP, make the configuration
> survive a redeploy, and turn the feature on in stages rather than all at once.
>
> For "a meeting is broken right now", go to [RUNBOOK.md](RUNBOOK.md). For recording specifically, go
> to [RECORDING.md](RECORDING.md) — recording is deliberately the **last** stage here and is gated on
> a decision no deployment step can substitute for.

**What was already true before this package.** Production has run LiveKit and Redis since 2026-09-04,
`wss://livekit.inksphere.space` is healthy, and a real two-device call with audio, video and screen
share has been completed. Those parts of "provision the topology" are done. What follows is what was
missing.

---

## 1. The gap this package closes

TURN was configured **UDP-only, with no domain and no TLS**:

```yaml
turn:
  enabled: true
  udp_port: 3478
```

That is enough for a home or mobile network, which is why the 2026-09-04 call worked and why
[RUNBOOK §4](RUNBOOK.md) could correctly record TURN as a red herring for *that* fault. It is not
enough for the networks TURN exists to serve. A corporate or hotel network that permits only
`443/tcp` outbound cannot reach **any** of the paths the deployment offered:

| Path | Port | Blocked by a UDP-only-egress firewall |
|---|---|---|
| WebRTC UDP | 7882/udp | yes |
| ICE/TCP | 7881/tcp | yes — non-standard port |
| TURN/UDP | 3478/udp | yes |
| TURN/TLS | *did not exist* | — |

So the honest statement of the old state is: **a participant behind a restrictive firewall had no way
into a meeting at all**, and nothing in the product would have explained why.

`dokploy.compose.yml` now adds TURN/TLS on **443**, terminated by Traefik and routed by SNI:

```yaml
turn:
  enabled: true
  domain: turn.inksphere.space
  external_tls: true
  tls_port: 443
  udp_port: 3478
  relay_range_start: 30000
  relay_range_end: 30100
```

**The one number that matters.** With `external_tls: true` LiveKit expects *unencrypted* traffic on
`tls_port` and still advertises `tls_port` verbatim as the `turns:` candidate it hands the browser.
The value is therefore the port a client **dials**, not an internal implementation detail. Setting it
to 5349 — the conventional TURN/TLS port, and the obvious-looking choice behind a proxy — would hand
every restricted client a candidate its own firewall rejects, and the deployment would look correctly
configured while failing exactly the users it was built for.

Binding 443 inside the container is safe because `livekit/livekit-server` declares no `USER` and runs
as root (verified against the upstream Dockerfile on 2026-09-07). If that ever changes the container
fails at startup with `bind: permission denied`, and the fix is
`sysctls: net.ipv4.ip_unprivileged_port_start=443` — **not** `cap_add`, which does not help because
Docker grants no ambient capabilities.

---

## 2. Stage 0 — provision TURN

Order matters: the certificate cannot be issued before DNS resolves.

**1. DNS.** Add `turn.inksphere.space` as an A (and AAAA, if the host has IPv6) record pointing at the
same Dokploy host as `livekit.inksphere.space`. Confirm before continuing:

```bash
dig +short turn.inksphere.space
```

**2. Firewall.** The host/cloud firewall must accept `443/tcp` (already open for Traefik), `7881/tcp`,
and `7882`, `3478` and `30000–30100` on UDP. Note from RUNBOOK §4 that `ufw` is not the authority
here — Docker publishes through its own chain and bypasses ufw's INPUT rules — so check the cloud
provider's security group, not just the host.

**3. Redeploy `meetings-media`.** The Traefik labels live in `deploy.labels`, which is where Traefik's
Swarm provider reads them. If this stack is ever switched to plain `docker compose`, they must move to
a service-level `labels:` block or TURN/TLS silently stays unrouted while everything else keeps
working.

**4. Verify the certificate exists.** Traefik requests it on first SNI match, so ask for it by name:

```bash
openssl s_client -connect turn.inksphere.space:443 -servername turn.inksphere.space </dev/null 2>/dev/null | openssl x509 -noout -subject -dates
```

A subject of `CN=turn.inksphere.space` and valid dates means Traefik holds a real certificate. A
self-signed or default Traefik certificate means the ACME challenge failed — almost always DNS not
resolving yet, or port 80 not reaching Traefik for the HTTP-01 challenge.

**5. Verify LiveKit is listening on the plaintext side.** From the host:

```bash
docker exec $(docker ps -qf name=livekit) sh -c "netstat -lnt | grep ':443'"
```

Nothing listening means the container failed the bind — check its logs for `permission denied`.

---

## 3. Stage 1 — prove TURN from a restrictive network

This is the exit criterion no configuration file can satisfy. It needs a real device on a real
restrictive network.

[`turn-check.html`](turn-check.html) is the harness. It connects to a real meeting with
`iceTransportPolicy: 'relay'`, which discards host and server-reflexive candidates outright — so if it
connects, it connected **through** the TURN server, because no other path was left. It also captures
the `iceServers` list LiveKit advertised, which answers a different and equally important question:
whether a `turns:…:443` candidate was offered at all.

**Run it:**

1. Serve the file over https (any static host; the frontend's `public/` works) — or open it from
   `file://` if you only need the ICE result and not a microphone publish.
2. Issue a fresh join token: `POST /api/meeting/{id}/join-token` as an assigned participant. Tokens
   are short-lived, so do this immediately before the run.
3. Open the page **on the restrictive network** — tethered to a corporate Wi-Fi, a hotel network, or a
   VPN that blocks UDP. Paste the URL and token, or send yourself a link with `#url=…&token=…` (the
   fragment is scrubbed from the address bar and never reaches a server log).
4. Press **Run check**, then **Copy evidence**.

**Passing looks like this** — all four checks green:

```
advertised: turn:livekit.inksphere.space:3478?transport=udp, turns:turn.inksphere.space:443?transport=tcp
connected:  true
relayed:    true (relay/tls -> host)
verdict:    PASS
```

`relayProtocol: tls` on the selected local candidate is the proof that matters: media crossed the
network over TURN/TLS on 443.

**A `PARTIAL` verdict** means the relay worked but not over `turns:443` — the network under test was
not actually restrictive enough to force it. Test from a stricter network before recording evidence;
a pass obtained over UDP proves nothing about the case this package exists for.

**Record the copied evidence block** in the MEETINGS.md evidence log, with the network described
("corporate guest Wi-Fi, 443/tcp egress only"). Run it at minimum on one desktop and one mobile
browser.

---

## 4. Stage 2 — make the configuration survive a redeploy

[RUNBOOK §2](RUNBOOK.md) records this as **not yet configured**, and it is the single most likely way
for a working deployment to break silently: values applied with `docker service update --env-add` live
outside Dokploy, and a redeploy rewrites the service spec from Dokploy's own configuration.

In Dokploy: **api → Advanced → Volumes → Add Volume → File Mount**. Confirm the app's `WORKDIR` with
`pwd` in the container terminal before trusting `/app`.

- **Mount path:** `/app/appsettings.Production.json`
- **Content** — both sections are required, because the mount replaces the file baked into the image:

```json
{
  "Meetings": {
    "Enabled": false,
    "GuestsEnabled": false,
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

The three `Meetings` flags start **false** on purpose: this file is the floor the deployment falls back
to, and the floor should be the safe state. Section 5 raises them one at a time through the service
environment, which takes precedence over this file wherever both are present. The mount is a floor,
not a conflict.

After configuring it, deploy once and confirm `/admin/settings` → **Meetings readiness** still reads
**Ready** without anyone running `docker service update`. That is what closes RUNBOOK §2, and it also
answers the open question there about whether Dokploy `v0.30.5` fixed the original injection bug.

---

## 5. Stage 3 — the staged flag rollout

Raise one flag at a time on the **api** service. After each, confirm readiness and exercise the
capability before moving on. Do not batch these: the whole point is that a failure names its own cause.

| Stage | Flag | Who it reaches | Proves |
|---|---|---|---|
| 3a | `Meetings__Enabled=true` | organization members only | lifecycle, join, chat, notes, files, archive |
| 3b | `Meetings__GuestsEnabled=true` | invited external people | access links, OTP, lobby, admission, eviction |
| 3c | `Meetings__RecordingEnabled=true` | hosts | consent, Egress, playback, deletion |

**Before 3a.** Readiness reads **Ready**; the TURN evidence from section 3 is recorded.

**During 3a — soak.** Run at least one week of ordinary internal use before 3b. Watch
`/admin/settings` → **Meetings health**; the eight alert rules and what each means are in
[docs/MEETINGS-OBSERVABILITY.md §5](../../docs/MEETINGS-OBSERVABILITY.md). Specifically check that
`room_finished` is not archiving meetings people are still in (the 2026-09-04 fault C regression) and
that webhook rejections stay at zero — a rejected webhook stops attendance being written while the
archive still renders correctly, so nothing on screen would tell you.

**Before 3b.** Guest access is the largest part of the attack surface;
[docs/MEETINGS-THREAT-MODEL.md](../../docs/MEETINGS-THREAT-MODEL.md) is the list of what was hardened
and what is accepted. Verify by hand that revoking an access link **ejects guests already in the
room** — that was a real defect (P7.2) and it is the one whose failure is invisible from the host's
side.

**Before 3c — this stage is not merely a flag.** [RECORDING.md](RECORDING.md) is the prerequisite list:
object storage credentials on both the api and `meetings-media` services, a deployed Egress worker,
and a capacity run. Phase 6 additionally gates production recording on a **staging run producing a
genuinely playable MP4** and a **jurisdiction-specific legal/product decision** on disclosure, consent
and retention. Consent is already enforced in the product — that is the mechanism, not the
authorisation. Do not raise this flag to "see if it works".

Note the current host has no headroom for Egress (it needs 4 vCPU / 4 GB of its own), so 3c also
implies either a larger host or a separate worker.

### Rollback

Every stage rolls back by setting its flag to `false` and redeploying; each is enforced server-side, so
lowering a flag closes the capability rather than merely hiding it. `Meetings__RecordingEnabled=false`
makes `MeetingRecordingFeatureFilter` return 404 for every recording route, and the Angular room backs
off after three failed polls. Roll back if: readiness leaves **Ready**, an observability alert fires
and is not understood within the incident window, guest sessions outlive a revoked link, or any
recording completes without a full consent record.

**Do not roll back by disabling `LiveKit__Enabled`.** That leaves `Meetings__Enabled` true, so meetings
remain listed and joinable-looking while every join refuses with `LiveKit media is not enabled` — the
exact confusing failure that deferred the rollout on 2026-09-02. Lower the `Meetings__*` flag instead.

---

## 6. Evidence this package must leave behind

Phase 7's exit criteria are met only when all of these are recorded in the MEETINGS.md evidence log
and the ProjectCompletion ledger:

- [ ] `dig` and `openssl s_client` output showing `turn.inksphere.space` resolving with a real certificate
- [ ] A `PASS` evidence block from `turn-check.html` on a genuinely UDP-blocked network, desktop and mobile
- [ ] A deploy completing with the File Mount in place and readiness still **Ready**, with no `docker service update`
- [ ] Production health and one synthetic meeting after stage 3a
- [ ] A backup taken and a restore rehearsed per [OPERATIONS.md](OPERATIONS.md) — still unexercised as of 2026-09-07
- [ ] The stage 3b guest verification, including link revocation ejecting a live guest
