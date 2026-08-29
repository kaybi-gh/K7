# Install and run

Production deployment with Docker Compose. Configuration details: [configuration.md](configuration.md). Day-to-day ops: [operating.md](operating.md).

## Prerequisites

- Docker Engine and Docker Compose v2
- A host directory or named volume for your media libraries
- (Recommended) A reverse proxy with TLS - see [Configuration - Reverse proxy](configuration.md#reverse-proxy)

## Quick start with the published image

Use the sample stack at the repo root:

- [`docker-compose.yaml`](../../docker-compose.yaml) - Postgres + K7 (`ghcr.io/kaybi-gh/k7:latest`)
- [`.env.example`](../../.env.example) - copy to `.env` and set `POSTGRES_PASSWORD` and `SECURITY__APIKEYS__HASHSECRET`

```bash
cp .env.example .env
# Edit .env and set POSTGRES_PASSWORD and SECURITY__APIKEYS__HASHSECRET

docker compose up -d
```

Open `http://localhost:7080` (or your reverse-proxy URL) and complete [first-run setup](#first-run-setup).

The sample sets `Security__ForceHttps=false` so the setup wizard works over plain HTTP. Behind a TLS reverse proxy, set `Security__ForceHttps=true` (and `BaseUrl`) - private Docker/LAN proxies are trusted by default. See [configuration.md](configuration.md#reverse-proxy).

For anything beyond a local trial (OIDC, federation, reverse proxy), set `BaseUrl` in compose to the public URL browsers and peers use - see [configuration.md](configuration.md#server-identity-and-http).

Point a library at `/media/movies` (or replace the `movies` volume in compose with a bind mount to your real media folder). To build the image locally instead of pulling GHCR: `docker build -t k7-server:latest .` then set `image: k7-server:latest` on the `k7-server` service.

For production, set a realistic Docker memory limit on `k7-server` (home setups often use `mem_limit: 2g`). See [Operating - Memory and container sizing](operating.md#memory-and-container-sizing).

The sample publishes Postgres on host port `5432` for convenience. On a public host, remove that `ports` mapping (or bind `127.0.0.1:5432` only) so the database is not reachable from the internet - see [configuration.md](configuration.md#hardening-checklist).

Sqlite is supported for small trials but is less performant than Postgres and not recommended for production - see [configuration.md](configuration.md#database).

## What the sample compose persists

| Volume / mount | Purpose |
|---|---|
| `k7-postgres-data` -> `/var/lib/postgresql` | Postgres database (PG 18+ parent path, not `.../data`) |
| `k7-data` -> `/data` | Server state: config, metadatas, logs, transcoding cache |
| `movies` -> `/media/movies:ro` | Example media library (Compose-managed named volume; swap for a bind mount in real use) |

Relative paths inside the container are **not** durable across recreate. Always use absolute paths under a mounted volume (the sample sets `Paths__*` to `/data/...`). See [Backup and troubleshooting](backup-and-troubleshooting.md).

## Container user (PUID / PGID)

The image runs as non-root. `entrypoint.sh` remaps `appuser` using:

| Env | Default | Purpose |
|---|---|---|
| `PUID` | `911` | Container UID |
| `PGID` | `911` | Container GID |

Set these to match the owner of your media mounts. The entrypoint also `chown`s `/data` and `/media` when those directories exist and are writable. You can set `PUID` / `PGID` in `.env` (see [`.env.example`](../../.env.example)).

## Hardware transcoding (optional)

The sample Compose file does not pass through a GPU. For Intel/AMD VAAPI mount `/dev/dri`; for NVIDIA NVENC use the NVIDIA Container Toolkit (`gpus: all`). Details and examples: [Operating - Hardware acceleration](operating.md#hardware-acceleration).

## First-run setup

Until setup completes, the server redirects non-API browser traffic to `/setup` and returns **503** for most `/api/*` routes.

You can finish first-run in any of these ways:

| Path | When to use | Setup token |
|---|---|---|
| **Password wizard** on `/setup` | Default local admin | Required when a token was generated (see logs / `K7_SETUP_TOKEN`) |
| **OIDC on `/setup`** | OIDC already enabled in config before first boot | **Not required** - completing IdP login creates the first Administrator |
| **Unattended env** | `K7_ADMIN_USERNAME` (or `K7_ADMIN_EMAIL`) + `K7_ADMIN_PASSWORD` | N/A (bootstrap skips the wizard) |

### Wizard (browser) - password

1. Open the server URL.
2. Create the first **Administrator** with a username + password (email is optional).
3. If a setup token field is shown, enter the token from the server logs. Look for a line containing `K7_SETUP_TOKEN=` (re-logged on every restart until setup completes), or set `K7_SETUP_TOKEN` before first start. The token stops anyone who can reach `/setup` from becoming admin without server access.

Password rules (Identity defaults): length at least 10, upper and lower case, a digit, at least 4 distinct characters.

### Wizard (browser) - OIDC

1. Configure OIDC (and usually `BaseUrl`) **before** or on first boot - see [configuration.md](configuration.md#oidc--sso).
2. Open `/setup` and use **Create admin with ...** (your IdP).
3. Sign in at the IdP. On success K7 creates the Administrator and marks setup complete - **no setup token**.

The first account that completes this OIDC flow becomes admin. Prefer an IdP you control (no open self-registration) if `/setup` is reachable on the network before setup finishes.

### Unattended bootstrap

| Mechanism | How |
|---|---|
| Env credentials | Set `K7_ADMIN_USERNAME` (preferred) or `K7_ADMIN_EMAIL`, plus `K7_ADMIN_PASSWORD`, before first start. Optional `K7_ADMIN_EMAIL` is stored when it looks like an email. |
| Setup token | Applies to the **password** wizard only (`K7_SETUP_TOKEN`, or auto-generated and logged) |
| Existing admin | If an Administrator already exists, setup is treated as completed |

After setup: create libraries and enable Guest from the admin UI. Registration and OIDC are config-only (env / `appsettings`) - the Authentication admin panel is read-only. See [configuration.md](configuration.md) and [operating.md](operating.md).

## Upgrades

On every startup the server applies pending EF Core migrations automatically.

1. [Back up](backup-and-troubleshooting.md) the database and `/data` (especially `Paths:Config`).
2. Read the GitHub release notes for breaking changes.
3. Pull and recreate:

```bash
docker compose pull
docker compose up -d
docker compose logs -f k7-server
```

If `k7-postgres` exits with a message about `/var/lib/postgresql/data` or `pg_upgrade --link`, see [Postgres 18 volume mount](#postgres-18-volume-mount).

Downgrading after newer migrations have been applied is **not supported** - restore from a pre-upgrade backup.

### Postgres 18 volume mount

The official Postgres 18 image stores the cluster under a versioned path (`/var/lib/postgresql/18/docker`) and expects a **single** volume at `/var/lib/postgresql`. Mounting the historical `/var/lib/postgresql/data` makes the container exit on start - including a **fresh** empty volume - with a journal message about an unused mount and `pg_upgrade --link`.

The sample [`docker-compose.yaml`](../../docker-compose.yaml) and the Helm bundled Postgres (`database.mode=deployment`) use the new path. CloudNativePG (`database.mode=cnpg`) and an external database are unchanged.

**Empty volume / first install that never started:** pull this compose (or chart) and recreate. You do not need a dump.

**Volume that already has a cluster** (Postgres 17 or earlier, or a 18 container that never started because of the old mount): changing the mount alone is not enough. Dump with the previous major, recreate an empty volume, then restore:

1. Stop the app. Keep the existing named volume.

```bash
docker compose stop k7-server postgres
```

2. Dump with a Postgres 17 container against the **old** mount layout. Compose names the volume `{project}_k7-postgres-data` (`docker volume ls`). Load `.env` so `POSTGRES_PASSWORD` is set.

```bash
docker run --rm -d --name k7-pg17-dump \
  -v PROJECT_k7-postgres-data:/var/lib/postgresql/data \
  -e POSTGRES_PASSWORD="${POSTGRES_PASSWORD}" \
  -e POSTGRES_USER="${POSTGRES_USER:-postgres}" \
  -e POSTGRES_DB="${POSTGRES_DB:-K7}" \
  postgres:17-alpine

until docker exec k7-pg17-dump pg_isready -U "${POSTGRES_USER:-postgres}"; do sleep 1; done

docker exec -T k7-pg17-dump \
  pg_dump -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DB:-K7}" -Fc > k7-before-pg18.dump
docker stop k7-pg17-dump
```

3. Confirm the dump file is non-empty. Then remove **only** the Postgres volume so 18 can `initdb` (the same volume remounted at `/var/lib/postgresql` still contains `PG_VERSION` at the root and will fail the same check). Do not use `docker compose down -v` - that also drops `k7-data`.

```bash
docker compose down
docker volume rm PROJECT_k7-postgres-data
```

4. Start Postgres 18 from the updated compose, restore, then start K7:

```bash
docker compose up -d postgres
# Wait until healthy, then:
docker compose exec -T postgres \
  pg_restore -U "${POSTGRES_USER:-postgres}" -d "${POSTGRES_DB:-K7}" --clean --if-exists < k7-before-pg18.dump
docker compose up -d
```

Keep `k7-before-pg18.dump` until you have signed in and confirmed libraries. Same idea for Helm bundled Postgres: dump, delete the PVC (or restore into a new one), upgrade the chart, restore. See [Backup and troubleshooting](backup-and-troubleshooting.md).

### Image tags

| Tag | Meaning |
|---|---|
| `latest` | Latest published non-prerelease |
| `x.y.z` | Exact semver |
| `x.y` / `x` | Floating major/minor from the release workflow |

Pin a semver tag in production. Breaking changes are called out in GitHub Releases (and `breaking-change` PR labels).

### Migrating from another media server

Use the import tool: [tools/K7.Import/README.md](../../tools/K7.Import/README.md).

**Back up the database first.** The import tool has no rollback; a failed or unwanted import is recovered by restoring that backup (see [Backup and troubleshooting](backup-and-troubleshooting.md)).

## Kubernetes (Helm)

The chart is published as an OCI artifact on GHCR: `oci://ghcr.io/kaybi-gh/charts/k7`.

```bash
helm install k7 oci://ghcr.io/kaybi-gh/charts/k7 \
  --version <x.y.z> \
  --set database.mode=cnpg \
  --set security.apiKeysHashSecret=<long-random-string>
```

Database is selected by `database.mode` (`charts/k7/values.yaml`):

| `database.mode` | Behaviour |
|---|---|
| `cnpg` | Chart provisions a Postgres `Cluster` and wires K7 to it. Requires the [CNPG operator](https://cloudnative-pg.io/). |
| `deployment` | Chart runs a single-replica bundled Postgres. Simple, not HA - trials/homelab. |
| `external` (default) | Set `database.external.host`, `.user`, and `.password` (or `.existingSecret`). |

Persist `/data` (config, metadata, logs, transcoding) via `persistence`, and mount media libraries read-only via `mediaVolumes`. Behind an ingress terminating TLS, set `security.forceHttps=true` and `baseUrl`.

Skip the setup wizard by bootstrapping the admin unattended: `--set setup.adminUsername=admin --set setup.adminPassword=<strong-password>`. Otherwise read the auto-generated setup token from the pod logs (`kubectl logs deploy/<release>-k7`), or pin it with `setup.token`.

Pod Security: the bundled Postgres ships `restricted`-compliant defaults. The K7 image starts as root (remaps `PUID`/`PGID`, chowns `/data`), so it is not `restricted`-compliant out of the box - `podSecurityContext` / `securityContext` are exposed in values for a `baseline` namespace or a non-root image.

## Non-Docker installs

**Supported production path: Docker (or another container runtime) using the published image.**

Running published binaries on bare metal is possible (.NET 10 runtime, ffmpeg, Postgres or Sqlite) but is not a documented or supported install mode.
