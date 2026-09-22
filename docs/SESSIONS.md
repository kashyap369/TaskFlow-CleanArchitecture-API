# TaskFlow — Session Log

## 2026-09-22 (SMTP wired to self-hosted mailcow; `noreply@inksphere.space` is the sender)

- **The "invites do not send yet" note in [SECRETS.md](SECRETS.md) is closed.** `EmailSettings`
  was still carrying the `smtp.gmail.com` / `noreply@example.com` placeholder, so organization
  invitations, email verification and meeting guest one-time codes all composed a message and
  dropped it. The API now points at the self-hosted mailcow (`2026-07b`) at
  `https://mail.buildbykashyap.in`. No route, no migration, no contract change.
- **The host is not the address's domain, and that is deliberate.** mailcow serves two domains
  from one box: `buildbykashyap.in` (personal — `contact@`, `dmarc@`, `newsletter@`, `shubham@`)
  and `inksphere.space` (product — `contact@`, `noreply@`, `shubham@`, `taskflow@`).
  **`mail.inksphere.space` has no A record**; only `mail.buildbykashyap.in` resolves. So
  `Host = mail.buildbykashyap.in` with `FromEmail = noreply@inksphere.space`. Anyone "correcting"
  the host to match the domain will take all outbound mail down.
- **Port 587, never 465.** `SmtpEmailSender` uses `System.Net.Mail.SmtpClient`, which has no
  implicit-TLS mode — `EnableSsl` there means STARTTLS. Port 465 *is* open on the host, so it
  presents as the more secure option and then hangs rather than failing cleanly.
- **DNS was verified rather than assumed**, against `1.1.1.1` and not the mailcow DNS page: MX
  → `mail.buildbykashyap.in`, SPF `v=spf1 mx ip4:72.61.231.225 ~all`, DKIM `dkim._domainkey`
  2048-bit RSA, DMARC `p=none` with reports to `contact@inksphere.space`, and PTR
  `72.61.231.225` → `mail.buildbykashyap.in` matching the HELO name. All correct.
- **Two accepted weaknesses, written down so they are not rediscovered as bugs.** DMARC is
  `p=none`, so a spoof of `inksphere.space` is not rejected by anyone — tighten only after the
  `rua` reports show TaskFlow's own mail passing, or the first casualty is your own invitations.
  And `inksphere.space` has **no sending history at all** (0 messages, 0 B); correct DNS does not
  buy reputation, so expect greylisting and spam-foldering from the large providers and warm the
  volume up instead of sending a batch.
- **The credential is an app password, not the mailbox password.** `EmailSettings__Username` /
  `__Password` come from Infisical; the password is scoped to SMTP on the `noreply@` mailbox, so
  revoking it neither locks anyone out of the mailbox nor hands a config leak the ability to read
  mail. Rotation order matters: create the new one, update the vault, restart, verify a send, then
  delete the old — deleting first fails every invitation for the length of the gap.
- **Not proven end to end.** No message has been sent. The `noreply@` mailbox has never completed
  a mail login (every `Last mail login` timestamp in mailcow is still blank), so the app password
  and the first real send remain outstanding.
- Also noted, not acted on: mailcow reports an available upgrade **2026-07b → 2026-09**.
- **Two sending identities, not one.** `noreply@inksphere.space` carries everything the user must
  act on (verification, sign-in and reset codes, org invitations, meeting invites and guest codes);
  `taskflow@inksphere.space` carries the thank-you and is replyable. `IEmailService.SendAsync` now
  takes an `EmailSender` with **no default**, so all six existing send sites had to name their
  identity — which is the point: which address a message leaves under is worth deciding where the
  message is written. The reason for the split is blast radius, not branding: a spam complaint
  about friendly mail must not be able to stop a reset code reaching a locked-out user.
- **The thank-you did not exist and is tied to verification, not registration.**
  `UserRegisteredEventHandler` sends *"Verify your TaskFlow account"* — the repo calls it the
  welcome email but it is the verification email. The new thank-you hangs off
  `UserEmailVerifiedEvent`, which was **already raised by `User.VerifyEmail()` and had no handler
  at all**. Two reasons for that placement: at registration the only thing the user needs is the
  verify link, and a second message competes with it; and verification is the point at which the
  address is proven real, which matters on a domain with zero sending reputation — mail to
  addresses that bounce is how a new domain earns a spam label. New template `ThankYou.html`
  (picked up automatically by the existing `Email\Templates\**\*.html` copy rule) and
  `UserEmailVerifiedEventHandler`, registered beside the other two handlers.
- **A failed thank-you cannot break verification.** Domain events dispatch after the commit, so a
  user who clicked a valid link must never be told it failed because a courtesy email did not
  send. The handler logs and swallows, rethrowing only genuine cancellation.
- **mailcow hygiene applied to both mailboxes.** Full name on each was `inksphere`, which is what
  recipients would have seen in the From line — both now `TaskFlow`. Quarantine notifications on
  `noreply@` went `Daily` → `Never` (nobody will ever read that mailbox, so the notices were mail
  to nowhere); `taskflow@` keeps `Daily` because it *is* read. Gotcha worth remembering: in the
  mailcow mailbox editor the quarantine toggle **saves itself immediately over AJAX** while the
  full-name field needs the form's *Save changes* — the first attempt silently kept the old name
  even though a green "changes have been saved" toast appeared.
- **Vault: 9 config keys written, both passwords deliberately left empty.** Production went 35 → 40
  secrets: 4 overwrites of the stale Gmail-era placeholders (`Host`, `Port`, `EnableSsl`,
  `Username`) and 5 new (`Transactional__*`, `Product__*`). `EmailSettings__FromEmail` and
  `__FromName` are now **dead keys** — they bind to nothing after the settings rewrite and should
  be deleted when convenient. The two app passwords are not set: creating them means typing a
  secret, which stayed with the owner.
- **No "Allow to send as" grant was made.** One app password *could* serve both addresses via that
  mailcow permission, and the settings support it through a fallback, but each mailbox keeping its
  own credential means neither can send as the other. Two app passwords was the deliberate choice.
- **`EmailSettings` has no `ValidateOnStart`**, unlike `JwtSettings`/`ClientSettings`/
  `ObjectStorage`. Mail configuration therefore cannot break the boot — it fails later, as mail
  that quietly never arrives. That is why this work is not finished until something is actually
  sent.
- **The vault's web console would not log in mid-session, then came back once signed in again.**
  While it was stuck it hung on the loading animation with `POST /api/v1/auth/token` → **404** as
  the only failing request. Worth keeping because the probe underneath it is durable and useful:
  `/api/status` **200**, `/api/v3/secrets/raw` **401**, `/api/v4/secrets` **404** — that last pair
  is the exact divergence the CLI pin exists for, confirmed still present. A stuck console is
  therefore not evidence that the vault is failing; check those three before concluding anything.
  **Do not reach for a restart or an upgrade**: that vault holds the only copy of the production
  secrets and the host has no backups.
- **`report-config.sh` now names both mail credentials.** It previously checked only
  `EmailSettings__Host`, so a dropped app password would have passed the boot report and then
  surfaced as mail that silently never arrived — the exact class of failure the script was written
  for. It prints names only, never values.
- **Five tests cover sender resolution** (`EmailSenderResolutionTests`), including the flat
  `EmailSettings__Product__FromEmail` binding the vault actually supplies and the single-credential
  fallback. Worth having because a wrong From address does not throw: mailcow refuses it with
  "not permitted to send as", long after the deploy looked healthy. Suite **118 → 123**.
- **Deploying the vault change without the code change would have broken sending.** The old
  `EmailSettings` has a flat `FromEmail`, which still held the Gmail address, so the API would have
  authenticated as `noreply@` and then tried to send as Gmail — refused by mailcow, with no startup
  error to warn anyone. Vault values and code must ship together.
- **Proven end to end: a real sign-in code was sent and delivered.** `config: present:` named both
  `EmailSettings__Password` and `EmailSettings__Product__Password` with no `MISSING:` line — and
  since the previous `report-config.sh` did not know those names, that one line proved both that
  the new build was running and that the vault values arrived. `POST /api/auth/login-code/request`
  then returned **200 in 2453ms**; because `OneTimeCodeRequestService` rethrows on a failed send, a
  200 can only mean the SMTP send completed, and 2.4s is a handshake rather than a timeout. The
  message reached Gmail **and landed in spam** — the predicted outcome for a domain with no
  sending history, not a fault.
- **The Docker hairpin works.** API and mailcow are the same host, so the container reaches mailcow
  via the host's own public IP through the bridge. That was the one path no local test could cover,
  and it is now known good.
- Ignore `libgssapi_krb5.so.2: cannot open shared object file` in the API log — Npgsql probing for
  Kerberos on a slim base image, unrelated to mail, predates this work.

### Carried to the next session (2026-09-23, deliberately deferred)

Nothing here blocks the transactional path, which is proven. Agreed to defer rather than forgotten:

1. **The `taskflow@` / `Product` sender has never sent a real message.** It is configured, its app
   password is in the vault and five unit tests cover the resolution, but nothing has left that
   mailbox in production. It only fires when a **brand-new account is verified**, and it uses a
   **different app password** from the one that was proven — so it can fail independently. Register
   a throwaway account and verify it.
2. **DKIM/SPF/DMARC were never confirmed on the delivered message.** The code landed in spam, which
   is the expected cold-domain outcome — but "Show original" in Gmail was not checked. If DKIM says
   FAIL, that is a real bug (mailcow not signing for `inksphere.space` despite the published key)
   and no amount of reputation warm-up fixes it. **Check this before assuming reputation.**
3. **The old Gmail app password is still live on Google's side.** Overwriting the vault row did not
   revoke it, and it was exposed in a screenshot. Remove it at myaccount.google.com.
4. **`EmailSettings__FromEmail` and `EmailSettings__FromName` are dead vault keys.** They bind to
   nothing after the settings rewrite. Delete them before someone edits them expecting an effect.
5. **Domain reputation.** `inksphere.space` has essentially no sending history. Register it with
   Google Postmaster Tools, keep volume low and to engaged recipients, and leave DMARC at `p=none`
   until the `rua` reports show TaskFlow's own mail passing consistently.
6. **mailcow update available**, 2026-07b → 2026-09. Nothing here depends on it.

Unrelated to email and still the largest risk on that host: the Infisical vault holds the **only**
copy of 41 production secrets and **the host has no backups**. Unchanged by this session.

## 2026-09-08 (Infisical vault stood up; all 35 taskflow secrets migrated and verified)

- **A secrets vault now exists and holds a verified copy of every taskflow credential.**
  Self-hosted Infisical at `https://vault.buildbykashyap.in`, project `taskflow`, environment
  `prod`. Full detail — layout, machine identities, cutover plan — is in the new
  [SECRETS.md](SECRETS.md). Nothing consumes it yet: **Dokploy is still the runtime source of
  truth**, and the API is unchanged. This session copied, it did not cut over.
- **Deliberately deployed outside Dokploy.** Running the vault as a Dokploy application would
  mean Dokploy holds the credentials for the vault that holds Dokploy's credentials; a broken
  deploy would take both. It is a standalone Compose stack at `/opt/infisical` under
  `infisical.service`, with **no published host ports** — Traefik reaches it over
  `dokploy-network` via a file-provider route. That last point matters on this host: UFW only
  admits 22/80/443, but Docker publishes through the `FORWARD` chain and **bypasses UFW
  entirely**, so ~120 ports are actually reachable. "Do not publish a port you do not need" is
  the only rule that holds here.
- **Dokploy stores `application.env` encrypted** (`iv:tag:ciphertext`, no readable key names),
  so its database is not a usable source for a migration. The authoritative source is the
  **resolved Swarm service spec** — what the container actually receives. Reading that gave 29
  variables for `taskflow-api`, matching the Dokploy UI exactly.
- **"All the credentials" was made falsifiable rather than asserted.** `buildArgs`,
  `buildSecrets`, `previewEnv`, `previewBuildArgs`, `previewBuildSecrets`, Dokploy's
  environment-level shared variables, and the `mongo`/`redis`/`mysql`/`mariadb` resource tables
  were each checked and confirmed empty for taskflow. Final diff: 35 source keys, 35 vault keys,
  zero missing, zero extra, **zero value mismatches**. Dokploy verified untouched afterwards via
  `UpdatedAt`, Swarm version indices, env counts, `.env` mtimes and container uptimes — all
  predating the session.
- **ROLLOUT.md §7 item 3 has fired. Meetings are broken right now.** The cross-check found
  `LIVEKIT_API_KEY` (what the LiveKit server accepts) and `LiveKit__ApiKey` (what the API signs
  join tokens with) hold **different values**, and `LiveKit__Url` resolves to that same
  container — there is no second deployment, so every token the API issues is rejected. The
  timestamps name the cause: `meetings/.env` modified 2026-09-07 **03:30:58**, then
  `taskflow-api` updated **03:38:58** with `state = rollback_completed`. The credential was
  rotated in the compose stack and the matching API deploy **failed and rolled back**, restoring
  the old key. This is the exact scenario ROLLOUT.md called "the one that quietly undoes today's
  work", and the same class as the 2026-09-02 propagation failure. It is no longer hypothetical.
- **Rotate the LiveKit key/secret when fixing this** — the current secret was exposed in a
  working transcript during the investigation and should be treated as compromised.
- Two copies of one credential drifted apart in production and nothing reported it. That is the
  case for a vault, made by the codebase itself on day one.

## 2026-09-07 (Meetings Phase 7 / P7.6 — TURN/TLS deployed and proven in production)

- **The deployment model was not what anyone had written down.** `meetings-media` is a Dokploy Compose
  service on the **Raw provider**, so the compose is pasted into Dokploy and no push to `main` has
  ever deployed it. Two commits earlier in the same day asserted the opposite (git + auto-deploy) and
  were corrected. Consequence worth keeping: `infra/meetings/dokploy.compose.yml` is a **reference
  copy that can drift**, and it already had — it carried an `egress` service production has never run.
  Pasting the repository file wholesale would have put a Chromium/GStreamer worker on a host
  RECORDING.md says lacks the headroom for one. Only the LiveKit changes were applied.
- **Reasoning from a UI absence produced a wrong conclusion.** The stack had stopped appearing in
  Dokploy's sidebar after the platform upgrade, and that was read as "Dokploy no longer manages it".
  It is a Compose service under Projects → Taskflow → production, running normally. Swarm keeps
  services alive regardless of what the panel lists; the panel is not evidence about deployment.
- **A certificate is not proof that a route works.** After the deploy, `CN=turn.inksphere.space`
  issued and it would have been easy to call the job done. Traefik terminates TLS at the edge, so the
  certificate says nothing about whether anything answers behind it. A **STUN Binding Request** needs
  no credentials, so a working TURN server replies to one: the response came back `01 01` with the
  transaction ID echoed, which proves client → TLS 443 → Traefik → LiveKit TURN end to end. Recorded
  as a reusable command in ROLLOUT.md §2 step 6.
- **The label-collision risk was real but did not fire.** Dokploy generates its own Traefik labels for
  the `livekit.inksphere.space` domain; hand-written `labels:`/`deploy.labels:` on the same service
  could have replaced them and taken down every meeting rather than just TURN. They coexist. The
  labels are declared in **both** places deliberately, because this Traefik runs the Docker *and*
  Swarm providers and they read different locations — idempotent, and it removes a silent failure mode
  where the router never registers at all.
- **Still open, and the one that quietly undoes this work:** the `appsettings.Production.json` File
  Mount is still not configured, and the **api** service *is* git auto-deployed — so a deploy can drop
  `LiveKit__*` and nothing reports it until someone is refused at join. ROLLOUT.md §7 is the ordered
  resume list.
- No code, no migration, ledger unchanged at `181/178`.

## 2026-09-07 (Meetings Phase 7 / P7.6 — production TURN topology and staged rollout)

- **The gap was a configuration nobody was wrong about.** RUNBOOK §4 recorded TURN as a red herring
  for the 2026-09-04 mobile fault — correct — and in the same sentence noted it was "UDP-only with no
  `domain`, which is genuinely incomplete". Both are true and independent. Clearing it as *that*
  incident's cause left it unfixed, so a participant on a network permitting only `443/tcp` had no
  path into a meeting at all: UDP `7882`, TURN/UDP `3478` and ICE/TCP `7881` are each blocked by such
  a firewall, and the product would have shown a bare connection failure.
- **`turn.tls_port` must be 443, and this is the trap.** With `external_tls: true` LiveKit expects
  plaintext on `tls_port` and **still advertises that number verbatim** as the `turns:` candidate it
  hands the browser. Behind a proxy the natural choice is 5349, which would advertise a port the
  restricted client's own firewall rejects — every local check passes and exactly the intended users
  fail. Verified against the upstream config sample and helm chart before writing it.
- **Binding 443 inside the container is only safe by accident of the base image.** `livekit-server`
  declares no `USER` and runs as root. Verified against the upstream Dockerfile rather than assumed,
  and recorded in the compose file, because a future non-root image turns this into a startup failure
  whose fix is `sysctl net.ipv4.ip_unprivileged_port_start=443` — `cap_add` does **not** work, since
  Docker grants no ambient capabilities.
- **The proof harness had to answer two different questions.** `infra/meetings/turn-check.html`
  connects with `iceTransportPolicy: 'relay'`, which discards host and srflx candidates, so a
  successful connection went through TURN with nothing else left to explain it. Separately it reports
  the `iceServers` LiveKit advertised — captured by wrapping `RTCPeerConnection` rather than reading
  livekit-client internals, which keeps it version-proof. "No `turns:443` was offered" and "the relay
  did not work" are different faults and now read differently.
- **The rollout file's fallback sets every `Meetings__*` flag to false.** A Dokploy File Mount is the
  floor a deployment lands on when its environment is dropped — the 2026-09-02 failure — so the floor
  is the safe state and the service environment raises flags above it. `ROLLOUT.md` also records that
  rolling back with `LiveKit__Enabled=false` is wrong: it leaves meetings listed and joinable-looking
  while every join refuses, which is that same failure wearing a different hat.
- No backend or frontend code changed; no migration; endpoint ledger unchanged at `181/178`. The
  harness was exercised locally against an unreachable endpoint — failure path, per-check reporting,
  verdict and evidence capture all render. **A `PASS` needs the host**: DNS, redeploy, certificate,
  and a device on a genuinely UDP-blocked network.

## 2026-09-07 (Meetings Phase 7 / P7.8 — retention reaches personal data)

- Closed the three gaps P7.7 documented, all inside `MeetingRetentionCleanupService` plus two new
  domain methods (`MeetingParticipant.RedactPersonalData()`,
  `MeetingAccessLink.RedactPersonalData(utcNow)`). No migration — P7.8 writes nulls into columns that
  already exist — and no route added or changed, so the ledger stays at `181/178`.
- **Redaction had to be a null, not a soft delete.** The obvious move was to `SoftDelete()` the
  participant row, and it would have been wrong twice: the row is referenced by attendance, messages,
  consents and the guest moderation trail, and `SoftDelete()` nulls no column — the email address
  would still have been bytes in PostgreSQL, which is the exact complaint P7.7 raised.
- **Widening the sweep created a hazard the old narrowness was hiding.** With cancelled/abandoned/
  `Live` meetings eligible, "expired" can describe a call in progress: a `Live` meeting has no
  `ActualEndUtc`, so its clock falls back to a `ScheduledEndUtc` or `UpdatedAt` that may be months
  old. Guard: skip a `Live` meeting with an open attendance interval. Worth remembering that
  *broadening a deletion rule is a change that can delete live data*, not only old data.
- **`UpdatedAt` is in the clock on purpose.** The archive-expiry rule uses
  `ActualEndUtc ?? ScheduledEndUtc ?? CreatedAt`; for a never-ended draft that would make the window
  run from the day it was created, so a draft edited weekly for a year would be swept while in use.
  `UpdatedAt` sits ahead of `CreatedAt` in the fallback chain so editing restarts the window.
- **Erasing more objects risks stalling on objects that never existed.** A `Failed` Egress may have
  written nothing, and a failed storage delete skips the meeting for every future pass too. So a
  missing object now counts as success (`FileNotFoundException` / `DirectoryNotFoundException`) while
  any other error still blocks and retries — and a pass that left meetings unfinished logs an
  **error** with the count instead of only per-object warnings.
- **Mutation-checked four ways rather than trusted.** Restoring `Ended`-only eligibility, restoring
  `Ready`-only object deletion, removing the redaction call, and removing the live-call guard each
  fail the new test. Backend `116/116`, build and EF drift clean.
- **Found in passing, not fixed here: the P7.7 frontend privacy-policy commit is not on frontend
  `main`.** `645ab14 docs(legal): cover meetings, calls and recordings in the privacy policy (P7.7)`
  exists only on the frontend branch `meetings/p7.3-capacity`; `main`'s `legal-documents.ts` has no
  Meetings section at all, so the published policy says nothing about meetings. Reported rather than
  merged — landing an unrelated branch in another repository is not this package's call. When it is
  landed, its sentence about invitee records being "retained beyond that point in a form that is no
  longer readable through the service" is now more cautious than the system behaves and wants a copy
  edit.

## 2026-09-06 (Meetings Phase 7 / P7.7 — privacy, retention, support and operations)

- Added `docs/MEETINGS-PRIVACY.md`, `docs/MEETINGS-SUPPORT.md` and `infra/meetings/OPERATIONS.md`,
  and extended the public privacy policy (frontend `legal-documents.ts`) with a **Meetings, calls and
  recordings** section plus a meeting-specific retention paragraph. Docs and one data-only frontend
  constant; no backend change, no migration, no route change.
- **The lesson of the package: writing a retention document is a way of testing retention.** Six
  facts came out of reading `MeetingRetentionCleanupService` line by line, and not one of them was
  visible from the roadmap:
  - The sweep deletes assets, recordings, consents, messages, notes, note revisions and attendance.
    It **never touches `MeetingParticipants` or `MeetingAccessLinks`** — which is exactly where the
    guest email addresses and the invited address live. Guest emails are the most sensitive thing a
    meeting collects and they are the one thing retention does not reach.
  - `AuditableEntity.SoftDelete()` sets two flags and nulls no column, so "retention deleted the
    chat" means the API cannot read it, not that PostgreSQL stopped holding it.
  - Only `Ended` meetings **with an `ActualEndUtc`** are considered, so cancelled, abandoned and
    stuck-`Live` meetings are permanent. (This is also why a post-restore stuck-`Live` meeting must
    be ended rather than left alone — it never gets a retention clock otherwise.)
  - Only `Ready` recordings have their object erased; a `Failed` one's partial artefact stays.
  - A failed object-storage delete `continue`s the loop, so the meeting is skipped and retried later
    — correct, but it means a storage outage silently keeps data past its declared window, and
    nothing alerts on it.
  - `OneTimeCodeSettings:SecretKey` falls back to `JwtSettings:SecretKey` and production leaves it
    unset, so **rotating the JWT key invalidates every guest OTP in flight.** Easy to trip over
    during an incident, when both rotation and guest logins are happening at once.
- **Decision: document the gaps, do not fix them here.** The package boundary in MEETINGS.md §1
  exists for this; the fix is proposed as P7.8. The privacy policy is worded to the behaviour that
  exists ("no longer readable through the service", records identifying who was invited retained
  beyond the window) rather than to the behaviour intended.
- The OPERATIONS.md procedures are **written but unexercised** — no restore or rotation drill has
  been run against production, and the document leads with that instead of implying otherwise.
- Verified: frontend typecheck, production build, `291/291` specs, lint, design lint, 42/42 contrast.
- Checked Dokploy (owner logged in) — the production environment has `api`, `frontend` and
  `meetings-media`. The API's environment values are masked and were left that way; the meaningful
  check for configuration propagation is the readiness panel at `/admin/settings`, which needs an
  admin login. Nothing in Dokploy was changed.

## 2026-09-06 (Meetings Phase 7 / P7.5 — critical E2E coverage)

- Added two tests to `TaskFlow.Tests/Api/PlannerApiIntegrationTests.cs` that drive the whole critical
  meeting journey and its refusal paths over real HTTP against a disposable PostgreSQL database, with
  only the media provider substituted: create → access link → guest OTP → admission → join tokens →
  attendance webhooks → chat/note/file → recording request → consent → Egress start/active/stop/
  complete → end → archive → playback; and separately, non-host recording refusal, unreadable-roster
  refusal, declined consent, and link revocation evicting a live guest.
- **The gotcha worth remembering: the integration fixture had never set `Meetings:RecordingEnabled`.**
  Every recording route was quietly answering `MEETING_RECORDING_DISABLED`, so the entire recording
  lifecycle and both playback routes had zero HTTP coverage — reachable only through handler tests,
  which substitute the provider, the object storage and the webhook pipeline all at once. That is the
  shape of gap to look for elsewhere: a feature flag that is off in the test fixture makes a whole
  surface *look* covered because the tests around it pass. Nothing was actually broken, but the
  composite-file storage key is written by one component and read by another and had never been
  checked to agree.
- **Decision: the fake provider's fault knobs are keyed by room name, not global flags.** One
  `IClassFixture` is shared by every test in the class, so a global "make the roster unreadable"
  switch leaks into whichever test runs next. Same reasoning applies to any future knob added there.
- **Both tests were mutation-checked before being believed.** Reverting the P7.2 roster fail-closed
  to an empty-roster fallback fails the denial test; changing the `EGRESS_COMPLETE` branch to mark
  `Processing` instead of `Ready` fails the journey test. Worth doing routinely for a test that
  passes on the first run against code it did not change.
- Backend `115/115`, build and EF drift clean, no migration, no route change — so
  `docs/ProjectCompletion.md` needed no edit and the ledger stays at `181/178`. No frontend change.
- **Not proven by any of this:** no browser, no real LiveKit, no two-device call, no load. Next is
  P7.6 (production LiveKit/Redis/TURN provisioning and staged rollout), which starts with the Dokploy
  File Mount task still sitting at the top of [PHASES.md](PHASES.md).

## 2026-09-05 (Meetings Phase 7 / P7.4 — metrics, traces, logs and alerts)

- Instrumented the meeting stack against a new [docs/MEETINGS-OBSERVABILITY.md](MEETINGS-OBSERVABILITY.md):
  one meter, `TaskFlow.Meetings`, plus eight alert rules with a runbook section each. The selection
  of what to count is the substance — every signal is a failure that **the screens keep looking
  healthy through**. A rejected webhook stops attendance being written while the archive still
  renders; a recording that never started leaves the host believing it did; a media stack that
  cannot be reached still hands out join tokens, because those are signed locally and never touch
  LiveKit.
- **The design decision worth remembering: identifiers are barred from metric tags but allowed on
  spans.** A meeting id as a tag gives every meeting its own time series and eventually takes the
  metrics backend down; the same id on a span belongs to one trace an engineer already has open. So
  metrics answer *that* meetings are failing and the trace answers *which one*. The same rule made
  the edge middleware tag the ASP.NET **route template** rather than the path — which also closes
  the hole where a caller mints unlimited series by inventing URLs, so unmatched paths are bucketed
  as `unmatched`.
- The privacy contract is a test, not a comment. `NoMeetingSignalCarriesMeetingContent` drives real
  handlers with a distinctive email, room name, meeting title and link token and fails if any of
  them reaches a tag. A documented rule about telemetry rots the first time someone adds a
  "temporary" tag; a failing test does not.
- Telemetry is emitted from Application and Infra, not only the API, so `MeetingTelemetry` lives in
  `TaskFlow.Application/Common/Observability/` rather than beside `PlannerTelemetry` in the API.
  Capacity refusals, join issuance, the guest funnel and the recording lifecycle are all decided in
  handlers; the LiveKit call outcomes in Infra.
- Alerts nothing evaluates are not alerts, and this deployment has no metrics collector — hence
  `GET /admin/meetings/health`, which keeps a bounded ring of one-minute buckets over the last hour
  and evaluates the same thresholds in-process. It reports its own two limits rather than letting an
  operator assume otherwise: the window is per instance, and `fullyObserved` stays false until the
  process has been collecting for a full hour, so a ten-minute-old process never claims a quiet one.
- **Test gotcha for anyone adding meeting tests:** the meter is process-wide, so any test that drives
  a meeting handler emits into every listening snapshot, including another test's. The classes that
  emit now share the `meeting-telemetry` xUnit collection (`MeetingTelemetryCollection`) so they run
  sequentially. Add a new telemetry-emitting meeting test class to that collection, or its
  neighbours' counts will be flaky in a way that looks like a real bug.
- Two C# details that cost time: `ReadOnlySpan` parameters cannot be captured by a lambda **or a
  local function**, so the tag lookup in the `MeterListener` callback has to take the span as an
  explicit argument; and the ring buckets have to store the minute they hold, or a bucket written an
  hour ago is counted again when the index comes back around (there is a test for exactly that).
- **Both defects this package found were in its own wiring, and only real HTTP showed them.** The
  snapshot was a plain singleton, so DI built it when the health endpoint was first *read* — it
  reported an empty window over an API that had been serving meetings for hours. It is a hosted
  service now. And the global exception handler is the outermost middleware, so a refusal thrown as
  a `ForbiddenException` passed through the meeting middleware with `Response.StatusCode` still at
  its default 200: every refused meeting request was counted as a success, which is exactly
  backwards for rules that alert on refusals. `ExceptionHandlingMiddleware.StatusCodeFor` is now the
  single mapping both read. The general lesson: instrumentation registered as a passive singleton
  starts when someone asks about it, and middleware that reads a response status has to know what
  is outside it.
- Backend `113/113`, frontend `291/291`, builds, lint, design lint, 42 contrast checks and EF drift
  pass. No migration. One route added, so the ledger is `181/178`.

## 2026-09-05 (Meetings Phase 7 / P7.3 — declared capacity and concurrency)

- Every meeting ceiling is now configuration read through `IMeetingPolicy` and refused server-side,
  with the numbers and their enforcement points in [docs/MEETINGS-CAPACITY.md](MEETINGS-CAPACITY.md).
  Each refusal names its limit, because the Angular client shows the server's message and nothing
  else — "you have reached the limit" without the limit is not actionable.
- **The finding worth remembering: read-then-write duplicate suppression is not idempotency.** Chat
  had suppressed duplicate sends by looking for the client message id first, which only ever caught
  a retry that arrived *after* the first send committed. A client retrying on a slow connection has
  both in flight: both read nothing, both write, and the unique index refuses one — and that caller
  was told their message failed while it was sitting in the room. The write path now treats a failed
  save as "did the winner land?" and reports it. The general rule: when a unique index is the real
  guarantee, the code must handle *losing* to it, not merely try to avoid the race.
- Deliberate non-goal, written into the docs rather than left implied: count-based ceilings are
  checked before the write and not under a lock, so simultaneous requests can leave a meeting one
  over. Locking every chat message to protect the last seat costs more than the seat. Exactness is
  left where it already exists — the partial unique index for one active recording per meeting.
- The participant ceiling counts only *active* seats. Removed, revoked and denied participants
  release theirs; holding them would slowly shrink every long-running meeting until nobody could be
  added. A guest whose email already holds a seat is re-admitted rather than counted twice.
- Threat-model A-07 and A-08 closed. Guest sessions and OTP challenges are access records, so meeting
  retention never touched them and the tables grew forever; they are now purged (hard-deleted — a
  soft delete would leave the growth it fixes) after `Meetings:GuestAccessRecordRetentionDays`. Guest
  *decisions* are kept: they are the moderation audit trail. The guest rate limit is three budgets,
  the session-scoped ones keyed by a **hash** of the session token, never the token itself.
- Gotcha for future tests: an in-memory `MeetingAccessLink` built through `meeting.AddAccessLink`
  has `MeetingId` 0 until EF fills the back-reference, so a handler that looks the meeting up by
  `link.MeetingId` finds nothing and reports an invalid code. Set it by reflection in the arrangement.
- Backend `98/98` (16 new; three drive real HTTP + PostgreSQL), frontend `287/287`. No migration —
  capacity is configuration. The concurrency test was checked by inverting the fix and confirming it
  fails, so it is not passing vacuously.

## 2026-09-05 (Meetings Phase 7 / P7.2 — threat model and abuse review)

- Wrote [docs/MEETINGS-THREAT-MODEL.md](MEETINGS-THREAT-MODEL.md) by reading the feature end to end
  rather than from the design docs, and fixed the eight defects that review found. It is deliberately
  structured so the *accepted* risks are as visible as the fixed ones — five of the nine are owner
  decisions from MEETINGS.md §12, not engineering choices, and they should not quietly become
  engineering defaults.
- **The finding worth remembering: revoking an access link evicted nobody.** `MeetingGuestSession`
  had no reference to the link it came from, so revocation could only stop *future* verifications
  while everyone already inside kept a live session. The lesson generalises — a credential derived
  from another credential has to record its parent, or the parent cannot be revoked.
- **Second: consent derived from webhook state is consent derived from something that can be late.**
  The recording consent set came from open attendance intervals, which webhooks write; a lost webhook
  made the host the only person asked. It now reads the provider's live roster and refuses to record
  at all when the provider cannot answer. Recording is the one place where failing closed is cheap.
- Dead end avoided: `UpdateMeetingParticipant` *looks* like a privilege-escalation path (its validator
  does not exclude `Host`, unlike the add-participant validator). It is not — `Meeting.UpdateParticipant`
  refuses host transfer and refuses to demote the creator, and badge ids are checked against the owning
  meeting. Domain invariants, not validators, are what make that surface safe; do not "fix" the
  validator and assume something was gained.
- `MeetingRoomModerationRules.TryParticipantId` is now the single parser for
  `m{meetingId}-p{participantId}-{nonce}` identities; the webhook handler had a private duplicate.
- Backend `82/82` with nine new tests, one per finding. One additive migration,
  `AddMeetingGuestSessionAccessLink`. No route added or changed, and the Angular repo needed no change
  — it branches on no meeting error code, only `MEETING_READINESS_META`.

## 2026-09-04 (Meetings: production media works; auto-end and recording infrastructure)

- `room_finished` no longer ends a meeting unconditionally. The room empties whenever the last
  participant leaves — including when a client fault ejected everyone seconds in — so it now
  requires one attendance interval of at least `Meetings:AutoEndMinimumSessionSeconds` (default 30).
  `Meeting.HasSubstantiveAttendance` owns the rule; attendance is still always closed.
- `IMeetingPolicy` carries the threshold across the layer boundary, since `MeetingSettings` lives in
  Infra and the webhook handler is in Application — same shape as `IMeetingReadinessProbe`.
- Added the Egress worker to `infra/meetings/dokploy.compose.yml`. Production had **no** Egress
  service, so recording could never have worked regardless of the feature flag. `shm_size: 1gb` is
  deliberate: Chromium crashes silently mid-recording on Docker's 64MB default.
- Gotcha for the next session: Dokploy `v0.29.14` does not inject service environment variables into
  the running container. Verify with `env | grep -i livekit` inside the container, not by reading the
  Dokploy UI, and re-apply with `docker service update --env-add` after any redeploy.
- Backend `73/73` including two new `room_finished` regressions; build and EF drift clean.

## 2026-09-04 (Organization Meetings Phase 7 — P7.1 readiness and ready-anytime creation)

- **Deferral lifted.** Inspected the Dokploy production environment for the `api` service: all eleven
  `LiveKit__*` / `Meetings__*` keys are present, `LiveKit__Enabled=true`, the URL is
  `wss://livekit.inksphere.space`, and that host returns `200 OK` over TLS. So the configuration that
  was missing on 2026-09-02 is now correct — whether the *running* container has it is exactly what
  the new readiness route answers, and it deploys with this push.
- `Meetings__RecordingEnabled=false`, which is the right posture: Phase 6's legal/retention decision
  is still open, and the server refuses recording regardless of what any client asks for.

- Added `GET /admin/meetings/readiness` (AdminOnly): an `IMeetingReadinessProbe` contract in
  Application, implemented in Infra where both `LiveKitSettings` and `MeetingSettings` live, behind a
  MediatR query so the controller stays thin. It reads configuration rather than the database, so it
  uses no Dapper connection — the Dapper rule is about database reads.
- The probe signs a throwaway one-minute join token to prove the process can actually issue
  credentials. Token signing is local, so it needs no LiveKit server and cannot hang; media
  reachability is deliberately left to the staging two-client call.
- **Secrets never leave**: the API key is reported as an 8-char SHA-256 fingerprint (enough to compare
  against what was set, not enough to reverse) and the API secret only as "configured" plus its length.
  A test serializes the whole report and asserts neither raw value appears.
- Gotcha worth keeping: `LiveKit:Enabled=false` passes every existing options validator, because being
  switched off is legitimate. That is exactly why the Dokploy failure was invisible — validation
  fails fast only when the flag is *on* and the rest is wrong. The probe covers the other case.
- Backend `71/71` (5 new), build and EF drift clean; no migration.
- Next package: P7.2 threat model and abuse review.

## 2026-09-01 (Organization Meetings Phase 6 — implementation complete, certification pending)

- Added immutable per-participant consent, host-only recording lifecycle, late-join consent gates,
  LiveKit room-composite Egress, idempotent webhook/recovery reconciliation and end-meeting stop.
- Added private member/guest archive playback, creator storage-first delete, retention cleanup, nine
  bound routes and additive recording migrations with a database-level single-active-recording guard.
- Backend build, 65/65 tests and EF drift pass. Angular passes 278/278 specs, production build,
  lint/design lint and all 42 contrast checks. Docker is unavailable on this workstation, so the real
  Egress MP4/capacity run remains a staging certification item; production recording also remains off
  until the jurisdiction-specific legal/product review is recorded.

## 2026-09-01 (Organization Meetings — production verification follow-up)

- Pushed backend Phase 5 commit `d397ea5`; the production collaboration panel subsequently loaded
  without the prior missing-route error.
- Created and started production meeting `#3` and added Shubham Kashyap as an admitted participant.
  This proves production create/assignment/lifecycle paths, but the room reports `LiveKit media is not
  enabled`, so PC/mobile audio-video cannot connect until the LiveKit/Redis/TURN service and production
  `LiveKit__*` configuration are enabled.
- Found a separate UI regression: unchecking scheduling leaves hidden start/end required validators
  attached and blocks ready-anytime submission. Scheduled creation works. Both items are assigned to
  Phase 7 rollout/hardening; no Phase 5 scope was reopened.

## 2026-09-01 (Organization Meetings Phase 5 — completed end to end)

- Added persist-first idempotent chat, optimistic shared-note revisions, private scanned files and a
  complete attendance/content archive for both registered and guest meeting sessions.
- Added `AddMeetingCollaborationArchive`, 18 member/guest routes and storage-first six-hour retention
  cleanup. A failed object deletion leaves records intact for retry; ended meetings are read-only.
- Disposable PostgreSQL proves retry deduplication, stale-note `409`, outsider asset denial, scanned
  file upload/download and complete ended archive reconstruction. Backend tests pass 62/62; build and
  EF drift pass. Angular passes 276/276 specs, production build, lint/design lint and 42 contrast checks.
  Phase 6 is READY.

## 2026-08-31 (Organization Meetings Phase 4 — completed end to end)

- Completed P4.2–P4.5: server-authorized mute/remove, signed LiveKit attendance webhooks, durable
  event receipts, connection-scoped reconciliation and the additive `AddMeetingWebhookReceipts`
  migration. Disposable PostgreSQL proves denial, replay safety, persistence and removal revocation.
- Official LiveKit Server `1.13.6` passed the standalone disposable health path. Two independent
  in-app browser contexts proved registered/guest presence, leave and fresh-token reconnect with real
  webhook delivery; the established Phase 0 harness remains the microphone/camera/screen-share proof.
- Backend tests pass 60/60; build and EF drift pass. The frontend room, full 275-spec suite, production
  build, lint/design lint and all 42 contrast checks pass. Phase 5 is READY.

## 2026-08-31 (Organization Meetings Phase 4 — P4.1 room-token regression proof)

- Completed the bounded P4.1 package without adding a new room capability. The disposable PostgreSQL
  HTTP test now proves that assigned members get room credentials, unassigned users are forbidden, and
  verified guests cannot receive credentials until the organizer admits them.
- The integration host uses test-only LiveKit signing settings, so this validates API authorization
  without a running media server. Each test client now has a distinct forwarded test IP, avoiding
  unrelated guest scenarios contending for one real rate-limit partition. The full backend suite passes
  59/59. P4.2 moderation and durable attendance remains next; Phase 4 is still IN PROGRESS.

## 2026-08-31 (Organization Meetings Phase 4 — room-access hardening)

- Corrected a privilege boundary in authenticated room-token issuance: a user who can manage a meeting
  but is not assigned to it is denied instead of inheriting the creator's host participant/token.
  Explicitly assigned organization members are admitted directly; guests retain verified,
  organizer-controlled admission.
- Backend tests pass 52/52. The full Phase 4 moderator, signed attendance webhook and multi-browser
  evidence gates are still outstanding, so Phase 4 remains IN PROGRESS.

## 2026-08-31 (Organization Meetings Phase 3 — secure guest access)

- Added hash-only private/reusable invitations, rotation, email delivery, meeting-specific OTP
  challenges and separate opaque guest sessions; no guest path issues a normal TaskFlow JWT.
- Added stable guest participants, optional exact-email account binding and audited organizer
  admit/deny/revoke/remove decisions. Revocation immediately invalidates active guest sessions.
- Added the `AddMeetingGuestAccess` migration and five isolated public guest routes plus access-link
  rotation. All 52 tests pass with disposable-PostgreSQL guest/session isolation coverage; EF reports
  no pending model changes. Phase 4 is READY.

## 2026-08-30 (Organization Meetings Phase 2 — frontend management handoff)

- Angular now consumes nine core meeting routes through lazy organization list/detail surfaces,
  validated create/edit, lifecycle actions and registered-participant access management.
- Meeting records derive into the existing Calendar mapper without duplicate `CalendarEntry` rows;
  state resets across organization switches and unauthorized controls follow API-authored authority.
- Frontend is 262/262 with build/lint/design/contrast/detector green. Four badge/link metadata routes
  remain intentionally staged for the guest-link work in Phase 3, which is now READY.

## 2026-08-30 (Organization Meetings Phase 0 — LiveKit feasibility)

- Pinned the self-hosted LiveKit/Redis stack and both SDKs, added the TaskFlow-owned
  `IMeetingMediaProvider` boundary, local Compose/config examples, and a development-only API probe.
- Proved five-minute least-privilege room tokens plus raw-body signed webhook validation and event-id
  replay protection; real LiveKit participant/track/room webhooks returned 200 from TaskFlow.
- The isolated Angular harness completed a two-context mic/camera/screen-share/disconnect/reconnect
  flow. Backend is 42/42 with no EF drift; frontend is 258/258 with build/lint/design/contrast green.
  Phase 0 is DONE and Phase 1 is READY.

## 2026-08-30 (Organization Meetings — plan approved)

- Added the canonical Phase 0–7 Meetings contract in `docs/MEETINGS.md` for secure registered/guest
  email access, separate access levels and display badges, custom Angular/LiveKit calls, persistent
  collaboration/attendance, consent-aware Egress recording and production hardening.
- Chose TaskFlow/PostgreSQL/object storage as the durable authority and LiveKit only for realtime
  transport/recording production; Phase 0 is READY and no implementation or API surface has landed.
- Future sessions can use “meeting status,” “complete next meeting phase,” or “continue meeting phase N”
  and must update the canonical status/evidence plus both repositories' phase/session documents.

## 2026-08-29 (Organization Calendar Phase 4)

- Added one organization-owned calendar aggregate for events, member leave and holidays, the
  `ManageCalendar` catalog permission, focused CRUD/window CQRS handlers and an additive migration.
- Defined the recurrence contract as None/Daily/Weekly/Monthly, interval 1–30 and optional inclusive
  end date. Reads expand occurrences inside a validated 366-day organization-scoped query window.
- Backend is 40/40 with real HTTP/PostgreSQL recurrence, all-day, organization-isolation and delete
  coverage; Release build and EF model-drift checks pass. Angular consumes all four new routes.

## 2026-08-29 (Organization Calendar Phase 3)

- Added `EstimateMinutes` / `WeeklyCapacityMinutes`, the additive capacity migration, and focused
  permission-gated writes without expanding the general task/member update contracts.
- Added a date-only, Monday-based Dapper query whose totals and workload state are computed in
  PostgreSQL. The test pass caught and fixed Dapper's unsupported DateOnly parameter binding while
  preserving DateOnly at the HTTP contract.
- Backend is 35/35 with real HTTP/PostgreSQL coverage for totals, UTC week edges, missing data and
  organization isolation; build and EF model-drift checks pass.

## 2026-08-29 (Organization Calendar Phase 2)

- Added the focused task schedule route/command/domain method instead of expanding general task update.
- Reused `ManageTasks`, rejected personal scheduling and validated target date >= start date; no schema
  or migration was needed.
- Added three scheduling tests; the full suite passes 30/30 when the existing one-time-code test secret
  is provided. Angular consumes the route with permission-aware drag/resize and failure rollback.

## 2026-08-28 (Planner Phase 23 — hardening and production rollout)

- Bounded scene size/depth/elements/strings and link schemes, optimized scene persistence, retained the
  latest 100 revisions, indexed revision reads, and validated upload signatures before scanning.
- Added server and client feature flags, Planner rate limits, traces/metrics, structured slow/error and
  mutation audit logs, private download headers, and explicit legacy browser-scene import with rollback.
- Backend is 27/27 and frontend is 240/240; builds, lint/design lint, Storybook, EF drift, large-scene
  performance, ownership/security integration coverage, and critical browser specs pass.
- Both production services were released through Dokploy; migration and live health evidence are tracked
  in ProjectCompletion.md.

## 2026-08-28 (Planner Phase 22 — immutable primary requirements and comparison)

- Added transactional Baseline 1 snapshots and persistence-boundary New/Changed/Removed auditing so
  normal API mutations cannot bypass requirement history; progress-only updates remain excluded.
- Added owner-authorized baseline/history/comparison routes plus Angular finalization, filters, reasons,
  immutable snapshot inspection, and field-level baseline/current differences.
- Real PostgreSQL HTTP coverage proves immutability, ownership, progress separation, additions, edits,
  removals, and filters. Backend is 22/22 and frontend is 236/236; builds/lint and EF drift pass.
- Phase 23 hardening, performance, observability, feature-flag rollout, and critical E2E flows are next.

## 2026-08-28 (Planner Phase 21 — notes, documents, and secure media)

- Added owner-scoped `PlannerResource`/`PlannerAsset` persistence and eight resource/file routes with
  private object storage, 25 MB/type limits, safe names, SHA-256, and a scanner extension point.
- Added Note/Link/Document canvas cards, creation and inspector flows, authorized preview/download,
  rename, unlink/relink retention, and explicit resource/object deletion; scene JSON stays binary-free.
- Added migration, domain tests, and disposable-PostgreSQL HTTP coverage. Backend is 21/21 and frontend
  is 234/234, with production build and EF model-drift checks green. Phase 22 is next.

> Append-only. 3–5 lines per session. Focus on gotchas, dead ends, and decisions — things git history doesn't capture.
>
> **Planner roadmap complete:** Phases 17–23 are delivered. Continue to treat [PLANNER.md](PLANNER.md)
> as the product/architecture contract and [ProjectCompletion.md](ProjectCompletion.md) as the release ledger.

## 2026-08-28 (Planner Phase 20 — admin-managed template library)

- Added fixed Project/Task/Subtask/Note/Document template contracts, Draft/Published/Archived lifecycle,
  immutable published versions, type-safe JSON fields/defaults, node snapshots, and migration.
- Added AdminOnly management/publication routes, member published-active reads, an admin library page,
  and a Planner picker that applies defaults and visual dimensions/colors to new linked cards.
- Archived templates disappear from the member picker while old nodes retain their version; Note and
  Document definitions remain visible but await Phase 21 resources. All verification gates pass.

## 2026-08-28 (Planner Phase 19 — linked work objects and live progress)

- Added stable PlannerNode links to canonical personal Project/Task/Subtask records and six
  owner-authorized workspace/node routes for atomic creation, editing, rehydration, unlinking, and deletion.
- Extended projects with problem statement, budget/currency, and approximate duration through
  `AddPlannerLinkedWorkItems`, preserving existing project-client update behavior.
- Added linked-card creation and automatic missing-card recovery, live backend-derived canvas labels,
  inspector editing, progress/counts, and explicit unlink-versus-delete actions in Angular.
- PostgreSQL HTTP integration proves cross-user denial, exact-once canonical creation, external status
  refresh, planning fields, and removal semantics. Backend tests are 15/15 and frontend tests are
  231/231; build, lint/design lint, Storybook, Impeccable detector, and EF model checks pass.

## 2026-08-28 (Planner Phase 18 — cloud persistence and concurrency)

- Added one owner-authorized primary Planner board per personal project, immutable scene revisions,
  stable node identities, migration backfill, scene/history APIs, ETags, and UTF-8 payload limits.
- Replaced browser authority with debounced cloud autosave while retaining ordered IndexedDB recovery;
  offline, failed, unavailable-recovery, embedded-file, and revision-conflict states are explicit.
- Hardened concurrent saves at both the aggregate and PostgreSQL unique-constraint boundary so two
  simultaneous stale writes produce one success and one 409 instead of a silent overwrite or 500.
- Verified migration/backfill and real HTTP ownership, restore, stale-tab, and concurrent-write paths
  against a disposable PostgreSQL database. Backend tests are 12/12; frontend tests are 230/230;
  build, lint/design lint, Storybook build, and EF model-drift checks pass.

## 2026-08-28 (Planner Phase 17 — immersive shell and project context)

- Moved `/member/planner` to its own authenticated full-viewport route so Excalidraw owns the complete
  `100dvw × 100dvh` browser surface instead of inheriting the member shell's width, padding, and scroll.
- Added compact project/progress/save/tool overlays, creator-owned project selection, remembered last
  project, loading/error/empty states, and an inline personal-project creation drawer.
- Kept scenes isolated by user and project in temporary browser storage; Phase 18 must replace this
  with server-authorized boards, revisions, recovery cache, and concurrency conflict UX.
- Desktop (1280×720) and mobile (390×844) component previews pass; frontend build, lint/design lint,
  and Storybook build pass. Five focused specs compile, but ChromeHeadless crashes in the host GPU
  sandbox before Jasmine executes.

## 2026-08-27 (Planner requirements and end-to-end roadmap committed)

- Added `docs/PLANNER.md` as the canonical context for every future Planner discussion.
- Defined the full-viewport project-scoped workspace, Excalidraw/TaskFlow source-of-truth boundary,
  cloud persistence and concurrency, canonical work-item links, admin-versioned templates, secure
  resources, immutable primary requirement baselines, and New/Changed/Removed comparison history.
- Scheduled implementation as `docs/PHASES.md` Phases 17–23. **Next implementation phase is Phase 17;
  no Planner code or schema change was made in this documentation session.**
- Updated OVERVIEW and ProjectCompletion so a new chat following normal documentation entry points
  discovers the committed Planner roadmap immediately.

## 2026-08-15 (Private personal projects + Docker-free development)

- Added nullable project organization ownership with a creator/title partial unique index and two
  personal-project endpoints. Project access now branches cleanly: organization membership/permission
  for organization projects, exact creator match for personal projects.
- Personal task creation accepts an optional project only when both are organization-free and owned by
  the caller. The existing task-scoped guard keeps subtasks and work logs private as well.
- Applied `AddPrivatePersonalProjects` to the configured development database. Live project → task →
  subtask tests passed; a second valid user and an admin both received 403 across ownership boundaries.
- Added a development-only local filesystem object-storage provider. Production continues to select S3;
  local API + Angular now run directly without Docker.

## 2026-08-15 (Project authorization + Individual organization access)
- Project create/update/delete handlers were the exception to the command-side authorization rule:
  they accepted every authenticated system role and never checked organization membership or the
  project permission catalog. Creation now requires `CreateProject`; update/delete require
  `ManageProjects`, with the standard owner bypass.
- Individual accounts already became real organization members after accepting an invitation, but
  the Angular portal guard treated account type as exclusive and made those memberships unusable.
  Individual accounts now retain `/member` as home while being allowed into joined `/organization`
  workspaces, with explicit switches in both layouts and membership refresh after acceptance.
- Backend build, frontend lint/build, and all 16 focused browser regressions pass. A broader run
  passed 215/215 specs after excluding the four-test public-header spec whose real navigation
  deliberately triggers Karma's existing full-page-reload disconnect.

## 2026-08-14 (Account recovery and passwordless login)
- Added persisted, purpose-scoped one-time codes for password reset and email-code login. Only an
  HMAC-SHA256 digest is stored; comparisons are fixed-time; codes expire in 10 minutes, are single
  use, lock after five failures, and enforce a 60-second resend cooldown.
- Request endpoints always return the same response for unknown, ineligible, and eligible accounts;
  SMTP failures are logged but do not become an account-enumeration signal. Four auth endpoints also
  have a shared per-IP rate-limit policy.
- Both login methods now share `IAuthSessionIssuer`, so JWT claims, refresh tokens, login audit time,
  and role behavior cannot drift. Password reset revokes every active refresh token.
- Migration `AddOneTimeCodes` was generated and applied locally. Build succeeds; pre-existing nullable
  warnings remain. Local API boot is blocked by missing ObjectStorage development configuration.

## 2026-07-26 (Phases 10–13 — Organization / Reporting / Admin to 100%)
- **Fixing one security hole uncovered a bigger one.** §4.3 said org update/delete had no
  authorization. While adding the owner check I looked at the neighbouring member commands and found
  **all four** (`Remove`/`Deactivate`/`Activate`/`ChangeMemberRole`) enforced *nothing* — any
  authenticated user could deactivate or remove any member of any org. It was live: the seeded admin,
  who belongs to no organization, could act on org 2's members. Logged as §4.3b. **Third time this
  family has appeared** (§4.1b in Phase 9 was the first). The tell is structural: when a command
  handler doesn't take `IOrganizationAccessGuard` or `IOrganizationPermissionChecker` in its
  constructor, it almost certainly enforces nothing — grep constructors, not method bodies.
- **`ChangeMemberRole` validated that the role existed but not that it belonged to the same
  organization.** A role id from another org was accepted, silently importing that org's permission
  set. Existence checks are not scope checks.
- **Owner-only needed a new guard method.** `EnsureOrganizationAsync` permits owner *or active member*
  — right for reading tasks, far too weak for renaming or deleting an entire workspace. Added
  `EnsureOrganizationOwnerAsync` rather than reusing the loose one.
- **The admin bypass is deliberately narrow.** §4.2 is fixed by short-circuiting `EnsureUserAsync` for
  a platform admin — *user profiles only*. It would have been one line to bypass
  `EnsureOrganizationAsync` too and turn the admin role into a skeleton key over every org's data.
  Didn't. `GET /user` was already AdminOnly, so this only makes the detail agree with the list.
- **Team assignment is its own route, not a field on `UpdateTaskCommand`.** This project has already
  been bitten twice by "the list DTO lacks the field, so saving the edit form blanks it" (task
  description, organization description). `PUT /task/{id}/team/{teamId}` + `DELETE /task/{id}/team`
  cannot be triggered by accident, and it matches how assign/unassign already work.
- **`TaskListSql` needed a LEFT JOIN, not a join.** `TeamId` is optional, so an inner join would have
  silently dropped every task without a team — which, before this phase, was all of them.
- **A setting nothing reads is worse than no setting**, because the UI implies it works. So
  `RegistrationOpen` is enforced in `RegisterUserCommandHandler` and `MaintenanceMode` in real
  middleware. Both **fail open** when the settings row is missing, matching pre-existing behaviour.
- **Maintenance mode needs escape hatches or it's a footgun.** `/api/auth/*` stays open and admins pass
  through everything — otherwise an admin flips the switch and locks themselves out of the very screen
  that turns it off. Both hatches verified live before trusting the feature.
- **`$pid` is read-only in PowerShell.** A verification script silently sent the *shell's* PID as a
  task id and produced two baffling 404s. Not an API bug — but worth knowing before debugging one.

## 2026-07-26 (Phase 9 completed — verification, reopen, and a NOT NULL bug the UI found)
- **The workspace worked but the *account lifecycle* didn't.** Auditing against OVERVIEW turned up two
  gaps nobody had listed: a completed task **couldn't be reopened** (`SubTask.Reopen()` existed,
  `Task` had none) and a **newly registered account could never sign in** — PendingVerification with no
  verification endpoint anywhere. The second one meant the Individual account was unreachable for a
  real user no matter how good the workspace was. Audit the promises, not just the endpoints.
- **Email verification with no schema change.** Used a **stateless HMAC token**
  (`userId.expiry.signature`, keyed with the JWT secret, 48h) instead of a token column — no migration,
  can't be forged, expires itself, and verifying twice is a no-op because `User.VerifyEmail()` already
  returns early. `UserRegisteredEventHandler` resolves the user **by email**: the event is raised inside
  `Register()` before the row exists, so it can't carry the id. Resend always returns 200 — replying
  "no such user" would make it an account-enumeration oracle.
- **🚨 The new UI immediately found a real bug: `TaskWorkLogs.Notes` was NOT NULL** while the domain
  wrote `notes?.Trim()`. Starting a timer without a note **500'd**. The org portal never hit it because
  its form always sent a string. **A non-nullable CLR `string` silently becomes a NOT NULL column** —
  exactly the earlier `RefreshToken.RevokedByIp` bug. Fixed: `string?` + `IsRequired(false)` + migration
  `MakeWorkLogNotesNullable`. Worth grepping the remaining entities for the same shape.
- **Reopen defers to the subtasks.** `Task.Reopen()` clears `ActualCompletionDate`, then calls
  `RecalculateStatus()` if the task has subtasks rather than forcing Todo — otherwise a task whose
  subtasks are all complete would flip to Todo and immediately disagree with its own children.
- Verified live end to end, twice: through the API, then through the browser as a **brand-new account
  created via the real sign-up form**.

## 2026-07-26 (Phase 9 SHIPPED — Individual account: personal workspace)
- **The whole feature was one nullable parameter plus a security fix.** `CreateTaskCommand.OrganizationId`
  `int` → `int?` was the only functional blocker; Domain, the DB column, `GetByTitleAsync`,
  `EnsureTaskAsync` and the read queries were already written for personal tasks. **No migration, no
  domain change, no schema change.** Survey the full stack before estimating.
- **9.0 first, and it earned its place.** 11 command handlers (task ×4, subtask ×5, work-log ×2) had
  **zero** authorization; `CreateTask` didn't even check you belonged to the org you were creating in.
  Proved it live: as a user in no organization, start/complete/delete/subtask/worklog/update against
  another org's task now all return **403** — every one succeeded before. Had personal tasks shipped
  first, that hole would have covered private data.
- **Reused `EnsureTaskAsync` rather than writing a write-side guard.** It already encoded exactly the
  right rule (org task → owner/active member; personal task → creator only). Called it directly in the
  handlers instead of marking commands for `AccessGuardBehavior`, keeping the documented "commands
  enforce their own permissions" convention and leaving the behavior read-only.
- **Two routes, one command.** `POST /task` now 400s (`ORGANIZATION_ID_REQUIRED`) instead of silently
  creating a personal task when the client omits the org id — that would have been invisible data
  corruption. `POST /task/personal` takes a request record with no OrganizationId/ProjectId *by
  construction*, so the trap can't be reintroduced.
- **Gotcha for the frontend:** `GET /worklog/mine` **requires** `?from&to`. Omitting them binds
  `0001-01-01` and returns `[]` rather than erroring — it looks like "no data" when it's "no window".
  Same for `/report/me`. Cost me a confused minute during verification.
- Verified end-to-end then **restored seed state** (org 2 back to its 2 tasks, all test rows deleted).

## 2026-07-26 (Planned Phase 9 — Individual account; audited OVERVIEW as a spec)
- **`OVERVIEW.md` was claiming a vision that isn't built.** It said the vision was "implemented and
  verified end-to-end" and listed "personal tasks (nullable org)" as done. Audited it line by line:
  **Organization ~85%, Reporting ~70%, Individual ~0%.** Corrected the file. An OVERVIEW that overstates
  is worse than none — it's the doc a new session reads first to decide what's left.
- **Surveyed before planning, and the survey changed the plan's size.** Domain (`OrganizationId` is
  `int?`, `IsPersonal`, `Assign()` refuses personal), the **DB column (already nullable — no migration)**,
  `TaskRepository.GetByTitleAsync` (already branches on null org), `EnsureTaskAsync` (personal → creator
  only) and the read queries are **all already built**. The Individual account is blocked by exactly one
  thing: `CreateTaskCommand.OrganizationId` being non-nullable. Trace the full stack before estimating —
  this looked like a feature and is closer to a parameter change.
- **🚨 Found a bigger problem while surveying:** `DeleteTask`, `StartTask`, `CompleteTask`,
  `CreateSubTask` and `StartWorkLog` handlers enforce **nothing at all** — no ownership, no org check, no
  permission. Any authenticated user can delete any task by id. `AccessGuardBehavior` doesn't cover it
  because it only inspects *reads*; the "commands enforce their own permissions" convention held for the
  org-permission-gated handlers but was never applied to these. Scheduled as **Phase 9.0, ahead of the
  feature** — shipping personal tasks onto an unguarded write side would mean private data anyone can
  mutate.
- **Decision: two routes, one command.** `POST /task` keeps requiring an org (400 without); a new
  `POST /task/personal` rejects org/project. A single nullable field would let a client bug that drops
  `organizationId` silently create a *personal* task instead of failing — invisible data corruption.
- Cross-project status now lives in **[ProjectCompletion.md](ProjectCompletion.md)** (API ⇄ UI parity
  ledger); update it whenever the API surface changes.

## 2026-07-23 (IDOR fix — read-side org scoping)
- Closed the IDOR gap: any authenticated user could read another org's data by guessing ids. Added `IOrganizationAccessGuard` (Infra/EF: owner or active member; resolves project/task/team/role → org; personal task → creator; user profile → self/shared-org; member report → self/owner-of-shared-org) and a MediatR `AccessGuardBehavior` that runs the check when a query implements one of the marker interfaces in `Common/Authorization/AccessScopedRequests.cs`.
- Chose the pipeline-behavior + marker-interface approach over editing 19 handlers: one line per query record, no handler changes, and it can't be forgotten as easily. Commands are NOT marked (they already enforce permissions).
- Verified live with a second user: seeded/verified `jane@example.com` via a throwaway Npgsql console (psql isn't installed; `dotnet ef` can't run arbitrary SQL). Result — admin (owner) 200 on org 1; jane (non-member) 403 on org/dashboard/tasks/team/role; jane self-profile 200 but admin-profile 403; jane "my" queries 200.
- Note: querying a non-existent org id now returns 403 (guard denies before the handler's 404) — intentional, avoids leaking existence. DB now contains test data (org "Acme Inc", a team, role, task; jane as a verified user).

## 2026-07-23 (Security pass — [Authorize])
- Applied `[Authorize(Policy = AllRoles)]` to every controller (org/work/team/worklog/report/user); `AdminOnly` on `UserController.GetAll`; AuthController left anonymous (register/login/refresh/logout — logout only needs the refresh token and `IpAddress` never throws). Chose AllRoles (not ManagerAndAbove as the old comments suggested) because an org owner may hold only the "User" system role — real org authz lives in the handlers via `IOrganizationPermissionChecker`.
- Verified: unauth → 401 on query/command/report endpoints; login open; admin reaches `/user/me` + AdminOnly list. Couldn't exercise the non-admin→403 path live (no verified non-admin user; psql not installed) — it's standard `RequireRole` behavior.
- **Flagged the next security item in PHASES.md: read-side org scoping / IDOR** — read queries taking an orgId/projectId don't verify the caller's membership yet, so any authenticated user can read another org's data by guessing IDs.

## 2026-07-23 (Application layer — write + read)
- Built the full Application layer for the vision. Write side: account type in registration; team commands; task assign/unassign (raise domain events); role grant/revoke permission; work log start/stop/manual/delete; `OrganizationMemberInvitedEventHandler` + Invitation.html template. New `IOrganizationPermissionChecker` (Infra, EF-based): owner bypasses, else active member's role must hold the permission — used by all org-permission-gated handlers.
- Read side (first queries in the project): chose Dapper-in-Application with `ISqlConnectionFactory` (Infra `SqlConnectionFactory` via Npgsql; replaced the empty `DapperContext` stub; added Dapper pkg to Application). Convention: query record + handler in ONE file under `Queries/{Name}/`, DTOs shared per entity. ~25 queries + 4 reports.
- Dapper gotchas handled: quote all PG identifiers, alias columns to DTO props, add `"IsDeleted"=FALSE` everywhere (EF filter doesn't apply), enums map from int, and `DateTime.SpecifyKind(...Utc)` before binding to `timestamptz` params (else Npgsql throws).
- **Pre-existing bug found during verification:** `RefreshToken.RevokedByIp`/`ReplacedByToken` were non-nullable strings (→ NOT NULL) but only set on revoke, so login's refresh-token insert 500'd every time. Made them `string?` + `IsRequired(false)`; migration `MakeRefreshTokenRevocationNullable`. Login works now.
- Verified live: login/JWT, profile, register w/ account type, org create + mine, team create (owner bypass), grant permission + role detail, task create, dashboard aggregate. `dotnet-ef` on PATH at `~/.dotnet/tools`; app on http://localhost:5138 (needs `ASPNETCORE_ENVIRONMENT=Development`). To run the app you must stop it before `dotnet build` (file locks on Windows): `taskkill //F //IM dotnet.exe`.

## 2026-07-23 (Infra layer)
- Implemented Infra for the new domain: `TeamRepository` (Include members), `OrganizationPermissionRepository`, `TaskWorkLogRepository` (running-timer lookup via `EndedAt == null`) — all matching existing repo style. Registered all three in `DependencyRegistration`.
- Added `OrganizationPermissionSeeder` (idempotent, syncs catalog from `OrganizationPermissionNames.All`), wired into `Program.cs` after RoleSeeder/UserSeeder.
- No new migration needed — `DomainVisionFoundation` (last session) already covered the schema; DB was already up to date. Installed `dotnet-ef` global tool is on PATH at `~/.dotnet/tools`.
- Verified end-to-end: app boots on localhost:5138, seeder inserts all 9 permissions, no errors. `OrganizationRolePermission` has no dedicated repo by design — it's owned by `OrganizationRole` (persisted via the aggregate; load with `GetByIdWithPermissionsAsync`).
- Note for next session: `dotnet run` from Api needs `ASPNETCORE_ENVIRONMENT=Development` for Swagger; app listens on http://localhost:5138.

## 2026-07-23
- Set up Claude Code documentation structure: CLAUDE.md + docs/ (OVERVIEW, ARCHITECTURE, CONVENTIONS, PHASES, SESSIONS).
- Analyzed full codebase and filled in all docs from actual code.
- Owner defined the product vision: two account types (Individual / Organization — enum already existed unused), org teams, permission-based roles, task assignment, projects, and a reporting dashboard as the headline feature. OVERVIEW.md rewritten; PHASES.md now has an 8-phase roadmap (account types → Dapper read side → teams/assignment → permissions → time tracking → reporting).
- Built the Domain layer for the vision (see PHASES.md status). Design decisions: personal task = `Task.OrganizationId == null`, duplicate titles scoped per-org for org tasks and per-creator for personal ones (`GetByTitleAsync` signature changed); team removal deactivates membership instead of deleting so reports keep history; permissions modeled as a global catalog table + role→permission join, granted via `OrganizationRole.GrantPermission`; `TaskWorkLog` supports both a live timer (StartNew/Stop) and manual after-the-fact entries. `User.Register` takes `AccountType` with Individual default so existing callers/rows stay valid.
- Installed `dotnet-ef` global tool (wasn't on this machine). Migration `DomainVisionFoundation` generated but **not applied** — run `dotnet ef database update --project TaskFlow.Infra --startup-project TaskFlow.Api`.
- New repo interfaces (ITeamRepository, IOrganizationPermissionRepository, ITaskWorkLogRepository) have no Infra implementations/DI registrations yet — that's Application/Infra phase work.
- Gotchas found: `DapperContext` and `Domain/Common/Result.cs` are empty stubs; no Queries exist yet (write side only); Org/WorkManagement controllers deliberately unauthenticated (dev stage); domain events dispatch synchronously after SaveChanges (SMTP failure throws after data persisted); response envelope `ApiResponse<T>` only used by AuthController.

## 2026-08-30 (Organization Meetings Phase 1 — authoritative core API)

- Added the meeting aggregate and constrained badge, participant, access-link and attendance
  foundations through the additive `AddMeetingCore` migration. Lifecycle rules cover
  Draft/Scheduled/Live/Ended/Cancelled, UTC schedules retain their display timezone, and the creator
  is an immutable Host.
- Added `CreateMeetings`, `ManageMeetings` and `RecordMeetings`, plus 13 feature-gated routes for
  bounded lists, authorized detail, lifecycle management and safe metadata. Access-link creation
  returns 256-bit random material once; only SHA-256 is persisted and later reads expose no secret.
- Preserved the provider boundary from Phase 0. Official token/grant and raw-body signed-webhook
  guidance was rechecked; this phase did not change LiveKit dependencies or issue production room
  tokens.
- Backend build, all 49 tests and EF model-drift checks pass. The disposable PostgreSQL HTTP suite
  proves participant archive access, outsider/cross-organization denial, lifecycle timestamps and
  one-time raw-link disclosure. Phase 2 is READY.

## 2026-09-02 (Organization Meetings deferred)

- Confirmed the public LiveKit endpoint and media container were healthy, but Dokploy `v0.29.14`
  retained the saved `LiveKit__*` values without propagating them into the API Swarm service.
- Per owner direction, stopped production meeting rollout work. The implementation and data remain
  intact for later Phase 7 resumption; the frontend sidebar entry is hidden in the sibling repository.
- Unrelated API functionality remains enabled. Do not resume Meetings until runtime configuration
  propagation and a real multi-client production call are verified.

## 2026-09-13 (Onboarding, the tour lock-out, permission locks, list controls)

- **The "website freezes" report was the tour, and it was exactly reproducible on paper.** driver.js
  adds `driver-active` to `<body>`, and `driver.css` turns that into
  `.driver-active * { pointer-events: none }` — the page takes no clicks except on the highlighted
  element and the popover. Nothing stopped a running tour on navigation, and the welcome's third
  step highlights `[data-tour="org.nav"]`, which is precisely what makes the sidebar clickable
  *during* the tour. Click a nav link, the page changes, the popover goes with it, and the class
  stays: every click and scroll swallowed, nothing on screen, reload the only way out. Fixed in
  `TourService` — teardown on `NavigationStart`, `destroy()` in a `finally`, and an unconditional
  sweep of driver's body classes and portalled nodes as a backstop.
- **Do not trust `localStorage` alone for a once-per-account promise.** The welcome was keyed on a
  per-user `localStorage` bucket, so clearing site data, a private window or a second machine all
  read as a new user. It now lives on `Users.OnboardingCompletedAt`. **The migration's backfill is
  the part that matters** — the column alone would have shown the welcome to every existing account
  on the next deploy.
- **The seeded admin password was `Admin@123git`**, not the `Admin@123` every doc quotes — a
  paste accident. Fixing the constant is not enough: `UserSeeder` only inserts when `Users` is
  empty, so a deployed database keeps the old hash forever. Credentials are now `Seed:Admin`
  configuration, with `ResetPasswordOnStartup` (default **false**) as the one-boot repair. It stays
  off by default on purpose — on every startup it would silently revert a password an administrator
  chose.
- **`background` shorthand vs. a drawn caret.** `_input.scss` strips native select chrome with
  `appearance: none` and draws the arrow back as a `background-image`; `_toolbar.scss` is imported
  later and set the `background` *shorthand*, which resets that image to `none`. Every `.list-filter`
  in the app had been rendering with no arrow at all. Use `background-color` when restyling a select.
- **A `<select>` needs more than a blocked click to lock.** `PermissionLockDirective` was built for
  buttons: `click` only, badge appended as a child. A select opens on `mousedown`, and only
  `<option>` may be its child. The directive now blocks `mousedown`, reverts a `change` that slips
  through, and puts the badge beside the control with the parent as the positioning context.
- Backend 118/118 and frontend 319/319 pass, both builds, lint, design lint and EF drift clean.
  **Deploy order matters:** the API migration must land before the UI, or `/user/me` has no
  `hasCompletedOnboarding` and the client (correctly) reads the absent field as "seen".

## 2026-09-13 — production 502 was a missing `JwtSettings`, not CORS

- **A browser "No 'Access-Control-Allow-Origin'" error is not evidence of a CORS bug.** Every call
  from `taskflow.inksphere.space` failed the preflight, but `api.inksphere.space/health` — anonymous,
  touching nothing — returned **502** too. Traefik generates that page itself and it carries no CORS
  headers, so Chrome reports the outage as a CORS failure. **Check an anonymous endpoint before
  touching CORS configuration**; two commits (`5f68cfe`, `bb33b45`) were spent adding and reverting a
  comment on a policy that was never wrong.
- **The real fault, from the Dokploy container log:**
  `InvalidOperationException: JwtSettings configuration is missing.` — container `Exited (139)` in a
  crash loop. `.Get<JwtSettings>()` returns null only when the section has **zero** children, so not
  one `JwtSettings__*` variable was reaching the container. `docs/SECRETS.md` records 29 variables for
  this app on 2026-09-08; 27 were present.
- **It threw at `Program.cs:200`, before `builder.Build()`** — outside the try/catch that exists to
  report startup failures cleanly. So the one class of error that handler was written for bypassed it
  entirely and died as an unhandled exception. `JwtSettings` now uses the
  `AddOptions().Validate().ValidateOnStart()` pattern the other five sections already use.
- **`appsettings.json` is in `.dockerignore`; `appsettings.Production.json` is not.** The non-secret
  JWT values (issuer, audience, lifetimes) now live in the Production file and ship in the image, so
  only `JwtSettings__SecretKey` has to come from the environment — four fewer variables that can go
  missing.
- **The integration tests were reading a developer's untracked `appsettings.json`** out of the test
  output directory for their JWT settings; the new validation is what exposed it. The fixture now
  declares them in its in-memory collection like every other section.
- Backend 118/118 pass, build clean. **The code fix does not end the outage on its own** —
  `JwtSettings__SecretKey` still has to be restored in Dokploy from the Infisical copy.

### Same session — Infisical cutover prepared (not yet completed)

- **The vault already held the missing secrets.** All five `JwtSettings__*` entries are in
  `taskflow` / `prod`. The outage was Dokploy losing its copy of configuration the vault still had —
  the argument for the cutover, demonstrated a second time in one week.
- **`Dockerfile` + `docker-entrypoint.sh` implement `infisical run`.** The entrypoint takes the vault
  path only when `INFISICAL_UNIVERSAL_AUTH_CLIENT_ID` is set and execs the API directly otherwise,
  so one image serves both sides of the cutover and rollback is unsetting three variables rather
  than a rebuild.
- **The cutover plan as written in SECRETS.md would not have booted.** `--fallback-enabled` is not a
  real flag; the credential env vars are `INFISICAL_UNIVERSAL_AUTH_*`, not
  `INFISICAL_MACHINE_IDENTITY_*`; `dl.cloudsmith.io` stops serving 2026-09-16; and exec-form
  `ENTRYPOINT` cannot do the command substitution the token fetch needs. All four corrected in the
  doc. **Verify CLI flags against the vendor's docs before committing them to a plan** — this one sat
  in the repo for five days reading as ready.
- **The offline-cache intent is now unmet and that is a real risk.** The vault runs on the same host
  as the API, so after cutover a vault outage stops the API from starting. Nothing in the CLI offers
  a documented fallback cache.
- **Not verified: the image build.** No Docker on the dev machine, so the `apt` install of the CLI
  and `infisical login` running as `$APP_UID` are both unproven. Watch the first build.

## 2026-09-13 — Infisical cutover completed, after a 50-minute production outage

The cutover landed, but not before the API was down from **09:52 to 10:43 UTC** returning 502 on
every endpoint.

- **The outage was step 5 run before step 3.** Dokploy's environment block was cleared down to the
  11 `LiveKit__*` / `Meetings__*` variables while the four `INFISICAL_*` credentials had never been
  added. The entrypoint took its documented fallback path, found an almost-empty environment, and
  died on the first database call: `The ConnectionString property has not been initialized`. The
  fallback behaved exactly as designed — the ordering in SECRETS.md is not advisory.
- **The `config:` diagnostic line from `report-config.sh` named the fault in one line** and turned
  what looks like a networking failure into a configuration one. It earned its place.
- **The image build was fine.** The "not verified" note from the previous session is resolved: the
  CLI installed correctly and `infisical login` works as `$APP_UID`.
- **The real defect was an unpinned `apt-get install infisical`.** CLI 0.43.0 moved secret fetching
  to `/api/v4/secrets`; the self-hosted vault's image was built **2025-08-08** and serves v3 only, so
  it answered 404 and `infisical run` exec'd the API with an empty environment — a *second*,
  identical-looking failure behind the first. Pinned to **0.42.6**, the last release using
  `/api/v3/secrets/raw`, with `infisical --version` at build time so a wrong version fails the build
  rather than the boot.
- **Diagnosing this needed no credentials.** `grep`ping the CLI binaries for `api/v[34]/secrets`
  across versions located the boundary exactly; two attempts to probe it with real credentials were
  correctly refused by tooling and were not necessary.
- **The vault is 13 months stale and is now a single point of failure.** Its container restarted
  2026-09-08 but reused an August-2025 local image, so the restart picked up nothing. With Dokploy's
  copies cleared, the vault holds the **only** copy of all 35 production secrets and still has no
  backups. Aligning the CLI down to the vault was chosen over upgrading the vault for exactly this
  reason: 13 months of migrations against an unbacked secret store, mid-outage, is not a trade worth
  making. **Back it up, then upgrade it** — that ordering is the open risk.
- **Cutover step 4 is satisfied by construction, not by the suggested test.** The container's own
  environment now holds only 15 variables, none of them `ConnectionStrings__*` or `JwtSettings__*`,
  yet the API reports every one as present. They can only have come from the vault.
- **Credentials exposed in a working transcript again** (LiveKit key/secret, the new Infisical client
  secret). Same class of leak as 2026-09-08. Rotation pending; the LiveKit pair needed rotating
  anyway because the two copies have diverged.
