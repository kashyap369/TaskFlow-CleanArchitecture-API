# TaskFlow — Secrets Management

How TaskFlow's credentials are stored, who can read them, and how the API will consume
them. Read this before changing any environment variable in Dokploy.

## Where the vault is

| | |
|---|---|
| Product | [Infisical](https://infisical.com) (self-hosted, open source) |
| URL | https://vault.buildbykashyap.in |
| Host | `72.61.231.225` — `/opt/infisical`, systemd unit `infisical.service` |
| Deployment | standalone Docker Compose, **deliberately outside Dokploy** |
| TLS | Let's Encrypt via the existing Dokploy Traefik, file provider at `/etc/dokploy/traefik/dynamic/infisical.yml` |
| Project | `taskflow` — ID `4203915e-62cd-4f00-bfd6-e0c16f319ab3` |
| Environments | `dev` / `staging` / `prod` (only `prod` is populated) |

Infisical runs outside Dokploy on purpose. If it were a Dokploy application, Dokploy would
hold the credentials for the vault that holds Dokploy's credentials — and a broken deploy
would take the vault with it. The vault sits *beneath* the platform, not inside it.

The project ID above is an identifier, not a secret. It is safe to commit.

## Current state (2026-09-08)

**The vault is a verified copy. It is not yet the source of truth.**

Dokploy still injects every environment variable at runtime exactly as before. Nothing in
the deployed API reads from Infisical yet. That change is the next phase, below.

### What was migrated — 35 secrets into `taskflow` / `prod`

| Source | Count |
|---|---|
| `taskflow-api-kf0oee` — Dokploy env (resolved Swarm service spec) | 29 |
| `taskflow-meetings-fyjooh` — compose `.env` | 3 |
| `shared-infrastructure-taskflow-postgres-dxkinh` — Dokploy env | 3 |

Each secret carries a comment recording its origin (`migrated from taskflow-api (dokploy swarm)`
and so on), so any entry can be traced back to where it came from.

### Deliberately not migrated

```
COMPOSE_PROJECT_NAME    # Docker Compose plumbing, belongs in the compose file
DOCKER_CONFIG           # Docker Compose plumbing, belongs in the compose file
```

`DOKPLOY_ENV_TEST` **was** migrated despite looking like leftover debris — a faithful copy
was preferred over a silent drop. Confirm and delete it.

### Sources checked and confirmed empty

So that "all credentials" is a claim with evidence behind it, each of these was checked:

- `application.buildArgs`, `buildSecrets`, `previewEnv`, `previewBuildArgs`, `previewBuildSecrets` — all zero-length
- Dokploy **environment-level shared variables** — zero-length across all four environments
- `taskflow-frontend-1afcti` — no environment variables at all
- Dokploy `mongo` / `redis` / `mysql` / `mariadb` resources — no taskflow entries

### Why the Swarm spec, not Dokploy's database

Dokploy stores `application.env` **encrypted** in its own Postgres (`iv:tag:ciphertext`;
no readable key names). The database is therefore not a usable source. The migration read
the **running Swarm service spec**, which is the resolved set of variables the container
actually receives — the authoritative runtime truth.

### Verification

Read back from the Infisical API and diffed against the live sources:

```
source keys : 35        vault keys : 35
missing from vault : NONE
extra in vault     : NONE
value mismatches   : NONE
```

Dokploy was **not modified**: service `UpdatedAt`, Swarm version indices, env counts,
`.env` mtimes and container uptimes were all confirmed unchanged afterwards.

## Known issue this surfaced: LiveKit credentials have diverged

The migration cross-checked the two copies of the LiveKit credential and found they hold
**different values**:

| Holder | Variable |
|---|---|
| LiveKit **server** (accepts only this pair) | `LIVEKIT_API_KEY` / `LIVEKIT_API_SECRET` |
| **taskflow-api** (signs join tokens with) | `LiveKit__ApiKey` / `LiveKit__ApiSecret` |

`LiveKit__Url` is `wss://livekit.inksphere.space`, which resolves to `72.61.231.225` — the
same container. There is no second deployment. **LiveKit will reject every token the API
issues**, so meeting joins fail.

The server timestamps show how it happened:

```
2026-09-07 03:30:58   meetings/.env modified            (new LiveKit keys set)
2026-09-07 03:38:58   taskflow-api updated              state = rollback_completed
```

The credential was rotated in the compose stack, and the matching API deploy **failed and
rolled back**, restoring the previous environment — including the old LiveKit key.

This is exactly the failure predicted in
[infra/meetings/ROLLOUT.md](../infra/meetings/ROLLOUT.md) §7 item 3 — *"nothing in the
product reports a dropped `LiveKit__*` environment until somebody is refused at join"* —
and the same class of problem recorded in SESSIONS.md on 2026-09-02. **It has now actually
occurred.** It is not hypothetical any more.

Fixing it is a prerequisite for any Meetings work. Two copies of one credential drifted
apart in production and nothing reported it; that is the argument for a vault, demonstrated.

> **Rotate the LiveKit credential when fixing this.** The current secret was exposed in a
> working transcript on 2026-09-08 and should be treated as compromised.

## Next phase — make the API read from the vault

Goal: Dokploy's environment block for `taskflow-api` shrinks from 29 variables to **four**,
and the vault becomes the single source of truth.

### Approach: `infisical run` in the container entrypoint

**Implemented** — `Dockerfile` + `docker-entrypoint.sh`, committed. The Infisical CLI fetches
secrets and execs the app with them in its environment. .NET's configuration binding is unchanged —
`JwtSettings__SecretKey` still arrives as an environment variable, so **no application code changes.**

The entrypoint runs the vault path only when a machine identity is present and execs the API
directly when it is not. That fallback is what makes the cutover reversible: unset the `INFISICAL_*`
variables and the container returns to Dokploy's environment block **without a rebuild**. It is also
what lets steps 3 and 4 below run with the old 29 variables still in place.

```sh
INFISICAL_TOKEN="$(infisical login --method=universal-auth --plain --silent)"
export INFISICAL_TOKEN
exec infisical run --projectId="$INFISICAL_PROJECT_ID" --env="${INFISICAL_ENVIRONMENT:-prod}" -- "$@"
```

Dokploy then holds only:

```
INFISICAL_UNIVERSAL_AUTH_CLIENT_ID=...
INFISICAL_UNIVERSAL_AUTH_CLIENT_SECRET=...
INFISICAL_API_URL=https://vault.buildbykashyap.in
INFISICAL_PROJECT_ID=4203915e-62cd-4f00-bfd6-e0c16f319ab3
```

`INFISICAL_API_URL` is read by both `login` and `run`, which is why neither needs `--domain`.

### Three corrections to the original plan (2026-09-13)

The first draft of this section would not have booted. Recorded so the errors are not reintroduced:

- **`--fallback-enabled` does not exist.** No such flag on `infisical run`, and no documented
  offline-cache flag either. The intent behind it was sound and remains **unmet**: the vault runs on
  the same host as the API, so it *is* a hard boot dependency. If the vault is down, the API will not
  start. Accept this knowingly, or give the vault its own uptime story before relying on it.
- **The credential variables are `INFISICAL_UNIVERSAL_AUTH_CLIENT_ID` / `_CLIENT_SECRET`**, not
  `INFISICAL_MACHINE_IDENTITY_*`. The CLI reads the universal-auth names.
- **`dl.cloudsmith.io` stops serving 2026-09-16.** The repository is now
  `https://artifacts-cli.infisical.com/setup.deb.sh`.

Also: `infisical run` cannot be the bare `ENTRYPOINT` as originally written, because the token must
be obtained first and exec-form `ENTRYPOINT` cannot do command substitution. Hence the script.

### Secret layout: flat, no folders

All 35 secrets live at the project root (`/`). Folders were considered and rejected:
`infisical run` reads path `/` and does **not** descend into subfolders without
`--recursive`, which is a silent way to lose variables. The .NET `__` prefixes
(`JwtSettings__`, `ObjectStorage__`, `EmailSettings__`) already namespace everything.

### Machine identities

| Identity | Scope | Role |
|---|---|---|
| `taskflow-prod` | `taskflow` / `prod` only | **read-only** |
| `migration-bootstrap` | `taskflow` (all) | Admin — **temporary, delete after cutover** |

Production never writes to the vault. Writes happen through the UI, by a human.

### Cutover order — do not skip step 4

1. Create the read-only `taskflow-prod` identity with Universal Auth
2. Add the CLI + entrypoint to the Dockerfile, build, deploy
3. Add the three `INFISICAL_*` variables to Dokploy — **leave the existing 29 in place**
4. **Verify the app is genuinely reading from the vault**: change a harmless value
   (e.g. `JwtSettings__ExpiryMinutes`) in Infisical, restart, confirm the new value takes
   effect. Until this passes, the app may still be reading Dokploy's copy — and you would
   be deleting the only working configuration.
5. Only then clear the 29 variables from Dokploy
6. Delete `migration-bootstrap` and its client secret

Given the rollback that caused the LiveKit divergence, watch step 2's deploy specifically —
a rolled-back deploy silently reverts the entrypoint change too.

## Cutover completed 2026-09-13 — and the two things it broke

The API now reads from the vault. Dokploy holds 15 variables: the four `INFISICAL_*` plus the 11
`LiveKit__*` / `Meetings__*` left in place. **Step 4 is satisfied by construction** — the container
environment contains no `ConnectionStrings__*` or `JwtSettings__*` at all, yet the API reports every
one present, so they can only have come from the vault.

Two faults, each of which alone took production down for 50 minutes:

1. **Step 5 was run before step 3.** Dokploy's environment was cleared before the `INFISICAL_*`
   credentials were added. The entrypoint fell back to the container environment exactly as designed,
   found nothing, and the API died on the first database call. The step ordering above is not advisory.
2. **The CLI install was unpinned.** CLI `0.43.0` moved secret fetching to `/api/v4/secrets`. This
   vault serves **v3 only** and answers `404`, so `infisical run` silently fetched nothing. The
   Dockerfile now pins `INFISICAL_CLI_VERSION=0.42.6` — the last release using `/api/v3/secrets/raw` —
   and runs `infisical --version` at build time so a wrong version fails the build, not the boot.

### The vault is stale, unbacked, and now the only copy

`infisical/infisical:latest-postgres` on this host was built **2025-08-08**. The container restarted
2026-09-08 but reused that local image, so the restart upgraded nothing. That 13-month gap is what
made the CLI pin necessary.

With Dokploy's copies cleared, **the vault is the only remaining copy of all 35 production secrets,**
and this host still has no backups. Upgrading it means running 13 months of migrations against that
single copy.

> **Back the vault up before upgrading it, and do not raise the CLI pin until `/api/v4/secrets`
> answers.** Raising the pin against this vault reproduces the 2026-09-13 outage exactly.

## SMTP — self-hosted mailcow (configured 2026-09-22)

TaskFlow sends invitations, one-time codes and guest access mail through the **self-hosted
mailcow** at `https://mail.buildbykashyap.in` (mailcow `2026-07b`). It is not a third-party
relay: the same box holds the mailboxes and does the sending, so its uptime is TaskFlow's
mail uptime.

### The two domains are not interchangeable

mailcow serves **two** domains from one host, and this trips people up:

| | `buildbykashyap.in` | `inksphere.space` |
|---|---|---|
| Role | personal / brand | product side — **TaskFlow's sender lives here** |
| Mailboxes | `contact@`, `dmarc@`, `newsletter@`, `shubham@` | `contact@`, `noreply@`, `shubham@`, `taskflow@` |
| `mail.<domain>` A record | `72.61.231.225` | **none — does not resolve** |

`mail.inksphere.space` has no A record. The SMTP **host** is therefore
`mail.buildbykashyap.in` while the **address** is `noreply@inksphere.space`. That mismatch is
correct and deliberate; "fixing" it to `mail.inksphere.space` breaks all outbound mail.

### Two sending identities, one server

TaskFlow sends as **two** addresses, and the split is deliberate — see `EmailSender` in
`Application/Contracts/Email`:

| | `EmailSender.Transactional` | `EmailSender.Product` |
|---|---|---|
| Address | `noreply@inksphere.space` | `taskflow@inksphere.space` |
| Carries | verification, sign-in and reset codes, org invitations, meeting invites and guest codes | the thank-you sent after a new account is verified |
| Replies | nobody reads them | reach a real mailbox |
| Used when | **default for anything new** | relationship mail, not pending-action mail |

The point is blast radius. If someone marks a friendly product email as spam, that must not be
able to stop a password-reset code reaching a locked-out user. Different mailbox, separate
reputation. A message sent under the wrong identity is not a cosmetic error.

`IEmailService.SendAsync` takes the sender with **no default**, so every send site states which
address it leaves under at the point the message is composed.

### Settings

| Key | Value | Where it lives |
|---|---|---|
| `EmailSettings__Host` | `mail.buildbykashyap.in` | Infisical |
| `EmailSettings__Port` | `587` | Infisical |
| `EmailSettings__EnableSsl` | `true` (STARTTLS) | Infisical |
| `EmailSettings__Username` | `noreply@inksphere.space` | Infisical |
| `EmailSettings__Password` | `noreply@` **app password**, SMTP-scoped | **Infisical — set by hand** |
| `EmailSettings__Transactional__FromEmail` | `noreply@inksphere.space` | Infisical |
| `EmailSettings__Transactional__FromName` | `TaskFlow` | Infisical |
| `EmailSettings__Product__FromEmail` | `taskflow@inksphere.space` | Infisical |
| `EmailSettings__Product__FromName` | `TaskFlow` | Infisical |
| `EmailSettings__Product__Username` | `taskflow@inksphere.space` | Infisical |
| `EmailSettings__Product__Password` | `taskflow@` **app password**, SMTP-scoped | **Infisical — set by hand** |

A sender that defines no `Username`/`Password` falls back to the top-level pair. That fallback
exists so the deployment *can* run on a single app password, but only if mailcow's
**"Allow to send as"** grants the authenticating mailbox the other address. That grant is
deliberately **not** in place: each mailbox holds its own credential, so neither app password can
send as the other.

**`EmailSettings__FromEmail` and `EmailSettings__FromName` are dead.** They survive from the
Gmail-placeholder era and bind to nothing — the properties no longer exist on `EmailSettings`.
Harmless, but delete them when convenient so nobody edits them expecting an effect.

**Port 587, not 465.** `SmtpEmailSender` uses `System.Net.Mail.SmtpClient`, which has no
implicit-TLS mode — its `EnableSsl` means STARTTLS. Port 465 is open on the host and will
appear to be a valid choice, but the client cannot speak it and the connection hangs rather
than failing cleanly.

### The passwords are app passwords, not mailbox passwords

Both password variables hold mailcow **app passwords** scoped to SMTP only. This matters twice
over: revoking one does not lock anyone out of the mailbox, and a leak of the API's configuration
does not hand over a mailbox that can *read* mail. Never put a mailbox login password in either.

Rotating one: create the replacement app password first, update Infisical, restart the API,
verify a real send, and only then delete the old one. Deleting first means invitations and guest
codes fail for the length of the gap.

**`EmailSettings` is not validated on startup** (unlike `JwtSettings`, `ClientSettings` and
`ObjectStorage`, which call `ValidateOnStart`). A wrong or missing mail value therefore cannot
stop the API booting — it surfaces later as mail that silently does not arrive. `SmtpEmailSender`
throws on an empty `FromEmail` for exactly that reason, but nothing checks the credentials until
the first send.

### Proven end to end 2026-09-22

A real sign-in code was requested against production and **arrived**. The chain that proves it,
in order, because each link rules out a different failure:

1. `config: present: ... EmailSettings__Password EmailSettings__Product__Password ...` with **no
   `MISSING:` line**. Those two names did not exist in the previous `report-config.sh`, so their
   appearance proves the **new build** is running *and* that both app passwords reached the
   container — one line, two answers.
2. `POST /api/auth/login-code/request` returned **200 in 2453ms**. `OneTimeCodeRequestService`
   **rethrows** on a failed send, so a 200 can only mean `SendMailAsync` completed. The 2.4s is
   the SMTP handshake: too slow to be a no-op, far too fast to be a timeout.
3. The message was delivered to Gmail — **into spam**, which is the expected outcome below and
   not a misconfiguration.

The container reaches mailcow over the **host's own public IP**, hairpinning out through Docker's
bridge, since the API and mailcow are the same machine. That path is now known to work; it is the
one thing no local test can establish.

### Deliverability posture

Verified 2026-09-22 against `1.1.1.1`:

- MX `inksphere.space` → `mail.buildbykashyap.in`
- SPF `v=spf1 mx ip4:72.61.231.225 ~all`
- DKIM `dkim._domainkey.inksphere.space`, 2048-bit RSA
- DMARC `p=none; rua=mailto:contact@inksphere.space; fo=1`
- PTR `72.61.231.225` → `mail.buildbykashyap.in`, matching the HELO name

Two standing caveats:

- **DMARC is `p=none`.** Nothing is enforced; a spoof of `inksphere.space` is not rejected by
  receivers. Reports go to `contact@inksphere.space`. Tighten to `p=quarantine` only after
  those reports show TaskFlow's own mail passing consistently — moving early will bin your
  own invitations.
- **`inksphere.space` has no sending history** (0 messages, 0 B as of 2026-09-22, and neither
  mailbox had ever completed a mail login). A cold IP on a fresh domain draws greylisting and
  spam-foldering from the large providers regardless of how correct the DNS is. Expect the first
  invitations to land badly and warm up gradually rather than sending a large batch on day one.
  The thank-you is tied to **verification** rather than registration partly for this reason: it
  only ever goes to an address that has already proved it receives mail.

## When the vault web UI will not log in (observed 2026-09-22)

The console hung on the Infisical loading animation and never rendered a login form. Signing in
again cleared it, so this is **not** a permanent fault — but the probe below is worth keeping,
because it distinguishes a stuck console from a failing vault. Probed from outside:

| Request | Status | Reading |
|---|---|---|
| `GET /api/status` | **200** | the backend is alive |
| `GET /api/v1/admin/config` | **200** | and serving |
| `GET /api/v3/secrets/raw` | **401** | the secrets API works — it only wants credentials |
| `GET /api/v4/secrets` | **404** | the divergence behind the CLI pin, still present |
| `POST /api/v1/auth/token` | **404** | **the only failing call, and where the UI hangs** |

**A stuck console is not a failing vault.** The API reads secrets through the pinned CLI over
`/api/v3/secrets/raw`, which answers `401` rather than `404` — the route exists and production is
unaffected. Check those three status codes before concluding anything: if `/api/status` answers
and `v3` returns `401`, the vault is serving and the problem is your session.

`/api/v4/secrets` returning `404` while `/api/v3/secrets/raw` returns `401` is the exact
divergence the CLI pin exists for, **confirmed still present on 2026-09-22** — so the pin must
stay. Whether the same divergence explains the missing `/api/v1/auth/token` is **inference, not
proof**: the route probe was stopped before it could be confirmed, and re-authenticating fixed
the symptom without establishing the cause.

**Do not fix this by upgrading or restarting the vault.** With Dokploy's copies cleared, it is the
only copy of all 35 production secrets and this host still has no backups, so a restart that fails
to come back is unrecoverable. The order stays the one already written above: **back it up, then
upgrade it.** A broken console is an inconvenience; a vault that does not restart is the end of the
project's credentials.

## Rotating a secret

1. Change the value in Infisical (`taskflow` / `prod`)
2. Restart the consuming service
3. If the secret has a second copy elsewhere — currently the LiveKit pair, which exists
   under two names — **update both**, then verify they match

Until the API reads from the vault, step 1 is not enough on its own: Dokploy still holds
the live copy and must be updated too. Two places, until cutover completes.

## Operational notes

- Signups are disabled instance-wide (`allowSignUp: false`). New users are added by invite,
  which needs SMTP — see the SMTP section below.
- `ENCRYPTION_KEY` in `/opt/infisical/.env` is unrecoverable if lost. Every secret in the
  vault becomes permanently unreadable, even with a full database dump. It belongs in a
  break-glass envelope stored off this server, alongside the root SSH key, registrar login
  and `mailcow.conf`.
- **There are no backups on this host yet.** The vault is currently one more thing that
  exists in exactly one place.
