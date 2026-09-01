# TaskFlow Meetings — local LiveKit and Egress

This stack is the local media and recording environment. It is not a production topology. Phase 6
adds a version-pinned Egress worker and shared private recording-output mount.

## Pinned components

| Component | Version | License | Purpose |
|---|---:|---|---|
| LiveKit Server | 1.13.6 | Apache-2.0 | Local realtime SFU and signed webhook sender |
| `livekit-client` | 2.22.1 | Apache-2.0 | Browser media client, isolated behind TaskFlow's room service |
| `Livekit.Server.Sdk.Dotnet` | 1.2.3 | Apache-2.0 | Token signing and raw webhook verification in Infrastructure |
| Redis | 8.2.9 Alpine | AGPL-3.0 | Local LiveKit coordination; no TaskFlow persistence |
| LiveKit Egress | 1.12.0 | Apache-2.0 | Room-composite MP4 recording into TaskFlow private storage |

These versions were checked against the upstream release pages on 2026-08-30. Recheck all four before
upgrading. TaskFlow's Application project depends only on `IMeetingMediaProvider`; LiveKit DTOs stay in
Infrastructure, and Angular LiveKit types stay in `MeetingRoomService`.

## Recording capacity and staging gate

Room-composite Egress runs Chromium plus GStreamer and is CPU intensive. TaskFlow's supported initial
ceiling is **one simultaneous 720p room-composite recording per 4-vCPU/4-GiB Egress instance**. Before
enabling recording in staging, run one 30-minute meeting with four publishers plus screen sharing,
verify the MP4, then repeat at the declared simultaneous ceiling while tracking Egress CPU, memory,
queue time, webhook completion, and object size. Reject rollout if CPU remains above 80%, memory above
85%, a job waits more than 30 seconds, or an output is missing/unplayable. Scale horizontally through
the shared Redis plane; do not raise the ceiling without new evidence.

The disclosure shown in both lobbies is a conservative product default, not legal advice. Production
`Meetings:RecordingEnabled` remains false until the target geography, retention/deletion policy, and
disclosure have been reviewed by the product owner and qualified counsel. Consent decisions are
immutable audit records; a decline or timeout prevents Egress from starting.

## Start

1. Copy the `LiveKit` block from `TaskFlow.Api/appsettings.Development.Example.json` into the ignored
   `appsettings.Development.json`.
2. Start the API with the `https` launch profile so both `https://localhost:7086` and the local webhook
   listener on `http://localhost:5138` are available.
3. From this directory run `docker compose up -d`.
4. Start Angular on port 4200 and open the probe page in two separate browser contexts.

The HTTP probe-group exception is development-only. Token issuance is loopback-only; the signed
webhook receiver remains reachable from the local LiveKit container. It is
needed because the LiveKit container cannot trust ASP.NET's self-signed local TLS certificate. All
production meeting endpoints and webhooks must use trusted HTTPS. On a host without an ASP.NET
development certificate, run the API's `http` profile and open the harness with
`?api=http://localhost:5138/api`.

## Smoke script

1. Join from both browsers and confirm each reports two participants.
2. Enable microphone and camera in both; confirm remote audio/video and local preview.
3. Share a screen from one browser; confirm it appears in the other, then stop it.
4. Disconnect one browser, confirm the other drops to one participant, then rejoin with a new API token.
5. Inspect the API response/log for a verified `room_started` or `participant_joined` webhook.
6. Re-submit one captured signed event during development and confirm `isDuplicate: true` on delivery
   two. Phase 1 replaces the in-memory replay guard with durable PostgreSQL receipts.

If Docker is unavailable, the exact LiveKit `v1.13.6` Windows binary can be run temporarily with
`--config infra/meetings/livekit-standalone-smoke.yaml`. This omits Redis only for the single-node
feasibility run; `compose.yml` remains the supported local team topology.

## Production assumptions recorded by the spike

- Use a dedicated trusted `wss://` LiveKit hostname; a normal HTTP reverse proxy alone is insufficient.
- Expose WebRTC UDP plus TCP/TURN fallbacks and account for public-IP advertisement/NAT behavior.
- Run Redis for production coordination. TaskFlow PostgreSQL/object storage remain authoritative.
- Use separate secrets per environment and rotate them through secret management.
- Deploy Egress separately only in Meeting Phase 6 and size it by simultaneous recording capacity.

## Dokploy production call stack

`dokploy.compose.yml` is the production audio/video stack for the existing Dokploy host. It deploys
one pinned LiveKit server and a dedicated persistent Redis, joins LiveKit to `dokploy-network` for
Traefik routing, and publishes WebRTC TCP `7881`, WebRTC UDP `7882`, TURN/UDP `3478`, and the bounded
TURN relay range `30000–30100/udp`. Configure `LIVEKIT_API_KEY` and `LIVEKIT_API_SECRET` in the
Compose environment.

After deployment, add `livekit.inksphere.space` in the Compose **Domains** tab with service `livekit`,
container port `7880`, HTTPS enabled, then point its DNS A/AAAA record at the Dokploy server and
redeploy the Compose stack. Ensure the host/cloud firewall accepts TCP `7881` and UDP
`7882`/`3478`/`30000–30100`.
The TaskFlow API uses `wss://livekit.inksphere.space` and the same key pair.

This host currently has insufficient safe headroom for Egress: the worker alone requires at least
4 CPUs and 4 GB RAM. Keep `Meetings__RecordingEnabled=false` here. Deploy `egress.yaml` on a separate
4-CPU/4-GB-or-larger worker (or upgrade the host) before enabling production recording.
