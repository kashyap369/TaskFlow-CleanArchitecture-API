# Enabling meeting recording in production

Recording is **off** in production (`Meetings__RecordingEnabled=false`). Turning the flag on alone
gives members a Record button that fails: LiveKit cannot record by itself. Egress is a separate
worker that joins the room headlessly, encodes a room-composite MP4 and uploads it to object
storage. Work through this list in order.

## 1. Object storage

The API writes recordings to a private S3-compatible bucket and streams them back to authorized
viewers; Egress uploads to that same bucket. Both need credentials.

Set on the **api** service:

| Variable | Notes |
|---|---|
| `ObjectStorage__Provider` | `S3` |
| `ObjectStorage__Endpoint` | e.g. `https://s3.eu-central-1.amazonaws.com`, or your MinIO/R2 endpoint |
| `ObjectStorage__Bucket` | private bucket — **must not** be public-read |
| `ObjectStorage__AccessKey` / `ObjectStorage__SecretKey` | scoped to that bucket only |
| `ObjectStorage__Region` | required by most providers |

Set the matching values on the **meetings-media** compose service, which the Egress config reads:
`OBJECT_STORAGE_ENDPOINT`, `OBJECT_STORAGE_BUCKET`, `OBJECT_STORAGE_ACCESS_KEY`,
`OBJECT_STORAGE_SECRET_KEY`, `OBJECT_STORAGE_REGION`.

## 2. Deploy Egress

`dokploy.compose.yml` now defines the `egress` service. Redeploy **meetings-media** and confirm the
container is running alongside `livekit` and `meeting-redis`. Egress needs `shm_size: 1gb` —
Chromium crashes silently mid-recording on the 64MB default.

## 3. Capacity

The compose caps Egress at 2 vCPU / 2 GB. The documented policy is **one 720p room-composite job per
~4 vCPU / 4 GB**, so this host supports roughly one concurrent recording. A partial unique index
(`EnforceSingleActiveMeetingRecording`) already prevents two active recordings on one meeting, but
nothing caps recordings across meetings — raise the limit only with a real capacity run behind it.

## 4. Turn the flag on

Set `Meetings__RecordingEnabled=true` on the **api** service, redeploy, and confirm
`/admin/settings` → Meetings readiness reports recording storage configured.

Note the flag is enforced server-side by `MeetingRecordingFeatureFilter`, which 404s every recording
route while it is false. The Angular room polls that endpoint, so a disabled feature is harmless —
the poll backs off after three failures.

## 5. Before real users record — not optional

Phase 6 of `docs/MEETINGS.md` gates production recording on evidence and approval that no code
change can substitute for:

- A staging run producing a **genuinely playable** room-composite MP4 at the declared capacity.
- The target geography's **legal/product decision** on disclosure, consent and retention, recorded
  in the evidence log.

Consent is already enforced in the product: every current participant must accept before a recording
starts, late joiners are gated, and a persistent indicator shows while recording. That is the
mechanism, not the authorisation.
