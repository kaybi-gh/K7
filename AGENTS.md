# K7 - Agent Instructions

Consolidated from `.github/copilot-instructions.md` and [docs/](docs/README.md).
Source of truth for Copilot remains in `.github/`; this file is the Cursor equivalent.

Human docs: [docs/README.md](docs/README.md). Setup and PRs: [CONTRIBUTING.md](CONTRIBUTING.md).

## Project

K7 is a self-hosted media server (music, movies, TV shows). .NET 10, C# 14, `TreatWarningsAsErrors`, nullable enabled, implicit usings, file-scoped namespaces.

## Architecture

Clean Architecture: **Domain -> Application -> Infrastructure -> Web (host)**.

| Project | Namespace | Role | May reference |
|---|---|---|---|
| `Server/Domain` | `K7.Server.Domain` | Entities, value objects, enums, events, interfaces | Nothing |
| `Server/Application` | `K7.Server.Application` | Use cases (CQRS), MediatR handlers, FluentValidation | Domain |
| `Server/Infrastructure/*` | `K7.Server.Infrastructure.*` | EF Core, filesystem, media processing | Domain, Application |
| `Server/Web` | `K7.Server.Web` | ASP.NET Core host, Minimal APIs, SignalR | All server layers |
| `Shared/K7.Shared` | `K7.Shared` | DTOs, constants | Domain (enums + `nameof` only) |
| `Clients/Shared` | `K7.Clients.Shared` | Services, models, interfaces | K7.Shared |
| `Clients/Shared/UI` | `K7.Clients.Shared.UI` | Pages, layouts, K7 components | Clients.Shared |
| `Clients/Web` / `Clients/MAUI` | ... | WASM / MAUI hosts | Shared + Shared.UI |
| `Clients/DesignSystem` | `K7.Clients.DesignSystem` | UI component catalog | Shared.UI |

Detail: [docs/dev/architecture.md](docs/dev/architecture.md).

## Code Style (all C#)

- Use `var` everywhere.
- File-scoped namespaces (enforced).
- Private fields: `_camelCase`. No `s_` prefix for static fields.
- Explicit accessibility modifiers on all members.
- Element ordering: fields -> constructors -> delegates -> events -> properties -> methods.
- Always forward `CancellationToken`. Last parameter. Public methods: `CancellationToken cancellationToken = default`.
- Prefer pattern matching (`is null`, `is not null`) over `== null`.
- Prefer `field` keyword (C# 14) over manual backing fields in properties.
- Structured logging only: `_logger.LogX("message {Param}", param)`. Never use `$""` interpolation.
- No em dashes, curly quotes, ellipsis, or other non-ASCII punctuation in code, comments, or documentation. Use plain ASCII: `-`, `...`, `"`, `'`.
- Run `dotnet format` to auto-fix formatting. See `.editorconfig`.

## CQRS / MediatR

Feature folder: `Features/{Feature}/Commands/{Name}/{Name}.cs` and `Features/{Feature}/Queries/{Name}/{Name}.cs`.
Request record + handler class live in the **same file**. Validators in `{Name}CommandValidator.cs`.
Domain event handlers in `Features/{Feature}/EventHandlers/`.

Pipeline behaviors (order): Validation -> Authorization -> UnhandledException -> Performance.

## Error Handling

Exception-based. Handlers throw typed exceptions; `CustomExceptionHandler` maps them to `ProblemDetails`:
- `ValidationException` -> 400
- `ForbiddenAccessException` -> 403
- `NotFoundException` -> 404

No `Result<T>` wrapper pattern.

## Key Patterns

- **DI**: Each layer has a `DependencyInjection.cs` with `AddXxxServices()` extension methods.
- **DTO mapping**: Extension methods in `Application/Common/Mappings/`. No AutoMapper.
- **Endpoints**: Minimal API in `Server/Web/Endpoints/`, thin; delegate to `ISender`.
- **Database**: EF Core multi-provider (Postgres + Sqlite). Never call `BuildServiceProvider()` during DI registration.
- **Domain entities**: Inherit `BaseEntity`, raise events via `AddDomainEvent()`.
- **Changes**: update tests and docs (`docs/user`, `docs/admin`, `docs/dev`) when behavior or public surfaces change. New shared UI components need a DesignSystem demo - [docs/dev/developing.md](docs/dev/developing.md#designsystem).

## Domain / Application / Infrastructure

- Domain: zero dependencies; events via `AddDomainEvent()`. Service contracts in Domain; `IApplicationDbContext` in Application.
- Application: commands/queries as above; queries use `AsNoTracking()`; data via `IApplicationDbContext`.
- Infrastructure: `IEntityTypeConfiguration<T>` under `Database/Context/Data/Configurations/`; migrations for **both** Postgres and Sqlite.

## Blazor UI (`src/Clients/**`)

| Area | Path |
|---|---|
| Pages | `Clients/Shared/UI/Pages/` |
| Layouts | `Clients/Shared/UI/Layout/` |
| Components | `Clients/Shared/UI/Components/` |
| Services | `Clients/Shared/Services/` |
| MAUI-only | `Clients/MAUI/` |
| Catalog | `Clients/DesignSystem/` |

**Triad:** `.razor` + `.razor.cs` + optional `.razor.css`. K7 component library only in pages (no third-party UI kits). Tokens / ThemeService; no hardcoded colors or inline styles. Localization mandatory (`IStringLocalizer` + `.resx`; French default, `*.en.resx` English; keys ASCII). Design constraints: [docs/dev/design.md](docs/dev/design.md).

## Testing (`tests/**`)

NUnit, FluentAssertions, NSubstitute; bUnit for critical components. Naming: `{ClassUnderTest}Tests`, `{Method}_Should{Expected}_When{Condition}`. Full project list and CI filters: [docs/dev/developing.md](docs/dev/developing.md#testing).

## Git Conventions

Conventional Commits: `type(scope): description`. Types: `feat`, `fix`, `refactor`, `perf`, `style`, `docs`, `test`, `chore`, `ci`, `build`.
Description in lowercase, no period at end. Subject line only - no commit body or multi-line description.
Never amend a pushed commit.

## Common Commands

```bash
dotnet run --project src/Shared/Aspire/AppHost   # Dev (Postgres, pgAdmin, DesignSystem, dashboard)
dotnet build -tl
dotnet test
dotnet format
```

## References

- `docs/README.md` - Documentation index
- `.github/copilot-instructions.md` - Copilot entry point
- `.github/instructions/*.instructions.md` - Path-scoped pointers to `docs/dev` (Copilot `applyTo`)
- `docs/dev/design.md` - UI design constraints
- `CONTRIBUTING.md` - Setup and contributing
