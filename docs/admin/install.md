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

Open `http://localhost:8080` (or your reverse-proxy URL) and complete [first-run setup](#first-run-setup).

For anything beyond a local trial (OIDC, federation, reverse proxy), set `BaseUrl` in compose to the public URL browsers and peers use - see [configuration.md](configuration.md#server-identity-and-http).

Point a library at `/media/movies` (or replace the `movies` volume in compose with a bind mount to your real media folder). To build the image locally instead of pulling GHCR: `docker build -t k7-server:latest .` then set `image: k7-server:latest` on the `k7-server` service.

The sample publishes Postgres on host port `5432` for convenience. On a public host, remove that `ports` mapping (or bind `127.0.0.1:5432` only) so the database is not reachable from the internet - see [configuration.md](configuration.md#hardening-checklist).

Sqlite is supported for small trials but is less performant than Postgres and not recommended for production - see [configuration.md](configuration.md#database).

## What the sample compose persists

| Volume / mount | Purpose |
|---|---|
| `k7-postgres-data` | Postgres database |
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

## First-run setup

Until setup completes, the server redirects non-API browser traffic to `/setup` and returns **503** for most `/api/*` routes.

### Wizard (browser)

1. Open the server URL.
2. Create the first **Administrator** account (email + password), or complete setup via OIDC if already enabled in config.
3. If a setup token is required, enter the token shown in the server logs (or set `K7_SETUP_TOKEN` before first start).

Password rules (Identity defaults): length at least 10, upper and lower case, a digit, at least 4 distinct characters.

### Unattended bootstrap

| Mechanism | How |
|---|---|
| Env credentials | Set `K7_ADMIN_EMAIL` and `K7_ADMIN_PASSWORD` before first start |
| Setup token | Set `K7_SETUP_TOKEN`, or let the server generate one (logged as a warning) |
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

Downgrading after newer migrations have been applied is **not supported** - restore from a pre-upgrade backup.

### Image tags

| Tag | Meaning |
|---|---|
| `latest` | Latest published non-prerelease |
| `x.y.z` | Exact semver |
| `x.y` / `x` | Floating major/minor from the release workflow |

Pin a semver tag in production. Breaking changes are called out in GitHub Releases (and `breaking-change` PR labels).

### Migrating from another media server

Use the import tool: [tools/K7.Import/README.md](../../tools/K7.Import/README.md).

## Non-Docker installs

**Supported production path: Docker (or another container runtime) using the published image.**

Running published binaries on bare metal is possible (.NET 10 runtime, ffmpeg, Postgres or Sqlite) but is not a documented or supported install mode.
