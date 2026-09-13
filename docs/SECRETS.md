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

## Rotating a secret

1. Change the value in Infisical (`taskflow` / `prod`)
2. Restart the consuming service
3. If the secret has a second copy elsewhere — currently the LiveKit pair, which exists
   under two names — **update both**, then verify they match

Until the API reads from the vault, step 1 is not enough on its own: Dokploy still holds
the live copy and must be updated too. Two places, until cutover completes.

## Operational notes

- Signups are disabled instance-wide (`allowSignUp: false`). New users are added by invite,
  which needs SMTP — currently unconfigured, so invites do not send yet.
- `ENCRYPTION_KEY` in `/opt/infisical/.env` is unrecoverable if lost. Every secret in the
  vault becomes permanently unreadable, even with a full database dump. It belongs in a
  break-glass envelope stored off this server, alongside the root SSH key, registrar login
  and `mailcow.conf`.
- **There are no backups on this host yet.** The vault is currently one more thing that
  exists in exactly one place.
