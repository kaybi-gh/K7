# Contributing guide

Thank you for contributing to K7. Opening a pull request does **not** guarantee a merge - see the README community section.

Full documentation index: [docs/README.md](docs/README.md).

## Architecture at a glance

K7 follows **Clean Architecture**: Domain -> Application -> Infrastructure -> Web. Clients are Blazor WASM + MAUI Hybrid sharing `Clients/Shared` + `Clients/Shared/UI`.

Diagrams and layer detail: [docs/dev/architecture.md](docs/dev/architecture.md).

## Prerequisites

- **.NET SDK 10.0+** - `dotnet --version`
- **Docker** - for Aspire (Postgres, pgAdmin), the Docker launch profile, and functional/integration tests (Testcontainers)
- **ffmpeg** - media processing (installed automatically in DevContainer/Docker)

## Developer setup

```bash
git clone <repo-url>
```

K7.Server.Web targets Linux at runtime for media processing (ffmpeg).

### DevContainer (recommended on Windows)

1. Open the repo in VS Code -> accept **"Reopen in Container"**
2. Run Aspire: `dotnet run --project src/Shared/Aspire/AppHost`

### Visual Studio - Docker profile (Windows)

1. Run Aspire once to create the persistent Postgres + pgAdmin containers, then stop with Ctrl+C (DB containers stay alive).
2. Select the **Docker** launch profile -> F5

Postgres via `host.docker.internal:5432` (credentials from Aspire parameters; default user/password are typically `postgres`/`postgres`). `host.docker.internal` is reliable on Docker Desktop for Windows/Mac; on Linux you may need an extra host mapping.

### Linux / WSL2

```bash
sudo apt-get update && sudo apt-get install -y ffmpeg
```

Install .NET 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux-ubuntu

### Windows native (no WSL/Docker)

Fine for non-ffmpeg-heavy flows; use Docker/DevContainer for full media processing parity.

## Running the application

### Aspire (recommended)

```bash
dotnet run --project src/Shared/Aspire/AppHost
```

Provides: Postgres (persistent, port `5432`), pgAdmin (http://localhost:5050), K7.Server.Web, DesignSystem (`k7-design-system`), Aspire dashboard. Database name: **`K7`**. Credentials: `postgres` / `postgres` (unless overridden by Aspire parameters).

EF migrations run at startup via `InitializeDatabaseAsync`.

### Production (Docker)

[docs/admin/install.md](docs/admin/install.md):

```bash
cp .env.example .env
docker compose up -d
```

Local image: `docker build -t k7-server:latest .` then set `image: k7-server:latest` on `k7-server`.

## Migrations

Always add migrations for **both** providers when the model changes:

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

At runtime, `MigrateAsync` applies pending migrations on startup. To reset a **dev** database, drop/recreate the Aspire Postgres volume or delete the Sqlite file, then restart.

Sqlite: `Database:Name` is a path **without** `.db`; the server appends `.db`.

## Build and test

```bash
dotnet build -tl
dotnet test
dotnet format
```

Projects, filters, Testcontainers: [docs/dev/developing.md](docs/dev/developing.md#testing).

## Client development

Web, MAUI, DesignSystem, localization, OpenAPI: [docs/dev/developing.md](docs/dev/developing.md).

UI constraints: [docs/dev/design.md](docs/dev/design.md). Screenshots / demo media: [docs/dev/releasing.md](docs/dev/releasing.md#demo-media-and-screenshots).

## Code style

Enforced via [`.editorconfig`](.editorconfig):

- Use `var` everywhere.
- File-scoped namespaces (enforced as warning).
- Private fields: `_camelCase`. No `s_` prefix.
- Explicit accessibility modifiers on all members.
- Always forward `CancellationToken` - last parameter, `= default` on public methods.
- Structured logging only: `_logger.LogX("message {Param}", param)`. Never use `$""` interpolation.
- Prefer `is null` / `is not null` and the C# 14 `field` keyword where appropriate.
- Plain ASCII punctuation in code and comments (`-`, `...`, quotes).

Run `dotnet format` to auto-fix formatting issues.

## Adding a new feature

Typical server feature:

1. **Command + Handler** - `src/Server/Application/Features/{Feature}/Commands/{Name}/{Name}.cs` (request + handler **same file**).
2. **Validator** - `{Name}CommandValidator.cs` (FluentValidation).
3. **Endpoint** - `src/Server/Web/Endpoints/{Feature}/{Name}.cs` (thin; `ISender`).
4. *(Optional)* Domain event handler under `EventHandlers/`.

Throw `NotFoundException`, `ValidationException`, or `ForbiddenAccessException` - mapped to `ProblemDetails` by `CustomExceptionHandler`.

Also:

- **Tests** for the new behavior (unit / bUnit / functional as appropriate).
- **Docs** if users or operators are affected (`docs/user`, `docs/admin`) or if developers need new guidance (`docs/dev`).
- **Shared UI components**: add or update a demo in DesignSystem - [developing.md](docs/dev/developing.md#designsystem).

## Pull requests

1. Branch from `main` (`fix/...`, `feat/...`, etc.).
2. [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): description` in lowercase, subject only (no trailing period). Types: `feat`, `fix`, `refactor`, `perf`, `style`, `docs`, `test`, `chore`, `ci`, `build`.
3. Ensure CI is green (`.github/workflows/build.yml` and related jobs).
4. Apply a **changelog label** (required by `pr-label-check.yml`): one of `breaking-change`, `enhancement`, `bug`, `chore`, `documentation`, `skip-changelog`.
5. Path labels (`server`, `clients`, `ci`, `tests`, ...) are applied automatically by the labeler.
6. Include **tests** and **documentation** updates with the change when behavior or public surfaces change.

Release tooling: [docs/dev/releasing.md](docs/dev/releasing.md).
