# Contributing guide

## Prerequisites

- **.NET SDK 10.0+** — `dotnet --version`
- **Docker** — for Aspire (Postgres, pgAdmin) or the Docker launch profile
- **ffmpeg** — media processing (installed automatically in DevContainer/Docker)
- **Essentia** — audio analysis, Linux-only, optional (soft-fails if missing)

## Developer setup

K7.Server.Web targets Linux at runtime (Essentia is a precompiled static binary downloaded from [essentia.upf.edu](https://essentia.upf.edu/extractors/)).

### DevContainer (recommended on Windows)

The simplest path — everything pre-installed (SDK, ffmpeg, Essentia, docker-in-docker):

1. Open the repo in VS Code → accept **"Reopen in Container"**
2. Run Aspire: `dotnet run --project src/Shared/Aspire/AppHost`

### Visual Studio — Docker profile (Windows + Essentia)

Full Essentia support with VS debugger:

1. Run Aspire once to create the persistent Postgres + pgAdmin containers:
   ```bash
   dotnet run --project src/Shared/Aspire/AppHost
   ```
   Stop with Ctrl+C — the database containers stay alive.
2. Select the **Docker** launch profile → F5

The `dev` Dockerfile stage provides the SDK + ffmpeg + Essentia. Postgres is reached via `host.docker.internal:5432` (pinned port, same `postgres`/`postgres` credentials).

### Linux / WSL2

```bash
sudo apt-get update && sudo apt-get install -y ffmpeg curl
curl -fsSL https://essentia.upf.edu/extractors/essentia-extractors-v2.1_beta2-linux-x86_64.tar.gz \
  | tar xz --strip-components=1 --wildcards '*/streaming_extractor_music' -C /usr/local/bin/
sudo mv /usr/local/bin/streaming_extractor_music /usr/local/bin/essentia_streaming_extractor_music
sudo chmod +x /usr/local/bin/essentia_streaming_extractor_music
```

Install .NET 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux-ubuntu

### Windows native (no WSL/Docker)

Everything works except Essentia audio analysis (silently skipped).

## Running the application

### Aspire (recommended)

```bash
dotnet run --project src/Shared/Aspire/AppHost
```

Provides: Postgres (persistent, port `5432`), pgAdmin (http://localhost:5050), K7.Server.Web (`--init-db`), Aspire dashboard with logs/traces. Credentials: `postgres`/`postgres`, database `k7`.

### Production

```bash
echo "POSTGRES_PASSWORD=your_secure_password" > .env
docker build -t k7-server:latest .
docker compose up -d
```

## Build & test

```bash
dotnet build -tl
dotnet test
```

## Code style

Enforced via [`.editorconfig`](.editorconfig) at the repository root.

## Migrations

```bash
# Postgres
dotnet ef migrations add <Name> \
  --project ./src/Server/Infrastructure/Database/Providers/Postgres \
  --startup-project ./src/Server/Web \
  -- --Database:Provider Postgres

# Sqlite
dotnet ef migrations add <Name> \
  --project ./src/Server/Infrastructure/Database/Providers/Sqlite \
  --startup-project ./src/Server/Web \
  -- --Database:Provider Sqlite
```
