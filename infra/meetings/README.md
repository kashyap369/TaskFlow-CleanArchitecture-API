# Meeting Phase 0 — local LiveKit probe

This stack exists only for the Phase 0 feasibility harness. It is not a production topology and does
not include Egress. The disposable Angular page is `http://localhost:4200/dev/meetings-livekit`.

## Pinned components

| Component | Version | License | Purpose |
|---|---:|---|---|
| LiveKit Server | 1.13.6 | Apache-2.0 | Local realtime SFU and signed webhook sender |
| `livekit-client` | 2.22.1 | Apache-2.0 | Browser media client, isolated behind TaskFlow's room service |
| `Livekit.Server.Sdk.Dotnet` | 1.2.3 | Apache-2.0 | Token signing and raw webhook verification in Infrastructure |
| Redis | 8.2.9 Alpine | AGPL-3.0 | Local LiveKit coordination; no TaskFlow persistence |

These versions were checked against the upstream release pages on 2026-08-30. Recheck all four before
upgrading. TaskFlow's Application project depends only on `IMeetingMediaProvider`; LiveKit DTOs stay in
Infrastructure, and Angular LiveKit types stay in `MeetingRoomService`.

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
