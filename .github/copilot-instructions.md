# K7 - Copilot Instructions

K7 is a self-hosted media server (music, movies, TV shows). .NET 10, C# 14, `TreatWarningsAsErrors`, nullable enabled, implicit usings, file-scoped namespaces.

## Architecture

Clean Architecture with strict dependency direction: **Domain → Application → Infrastructure → Web (host)**.

| Project | Namespace | Role | May reference |
|---|---|---|---|
| `Server/Domain` | `K7.Server.Domain` | Entities, value objects, enums, events, interfaces | Nothing |
| `Server/Application` | `K7.Server.Application` | Use cases (CQRS), MediatR handlers, FluentValidation | Domain |
| `Server/Infrastructure/*` | `K7.Server.Infrastructure.*` | EF Core (Postgres + Sqlite), file system, media processing | Domain, Application |
| `Server/Web` | `K7.Server.Web` | ASP.NET Core host, Minimal API endpoints, SignalR hub | All server layers |
| `Shared/K7.Shared` | `K7.Shared` | DTOs, constants shared across client and server | Domain (enums + `nameof` only) |
| `Clients/Shared` | `K7.Clients.Shared` | Client services, models, interfaces | K7.Shared |
| `Clients/Shared/UI` | `K7.Clients.Shared.UI` | Pages, layouts, K7 components | Clients.Shared |
| `Clients/Web` | `K7.Clients.Web` | Blazor WebAssembly host | Clients.Shared.* |
| `Clients/MAUI` | `K7.Clients.MAUI` | .NET MAUI Blazor Hybrid host | Clients.Shared.* |
| `Clients/DesignSystem` | `K7.Clients.DesignSystem` | UI component catalog | Clients.Shared.UI |

## CQRS / MediatR Conventions

Feature folder structure: `Features/{Feature}/Commands/{Name}/{Name}.cs` and `Features/{Feature}/Queries/{Name}/{Name}.cs`.
Request record + handler class live in the **same file**. Validators go in a companion `{Name}CommandValidator.cs`.
Domain event handlers go in `Features/{Feature}/EventHandlers/`.

MediatR pipeline behaviors (in order): Validation → Authorization → UnhandledException → Performance.

## Error Handling

Exception-based. Handlers throw typed exceptions; `CustomExceptionHandler` maps them to `ProblemDetails`:
- `ValidationException` → 400
- `ForbiddenAccessException` → 403
- `NotFoundException` → 404

No `Result<T>` wrapper pattern.

## Key Patterns

- **DI**: Each layer has a `DependencyInjection.cs` with `AddXxxServices()` extension methods.
- **DTO mapping**: Extension methods in `Application/Common/Mappings/` (e.g., `entity.ToLibraryDto()`). No AutoMapper.
- **Endpoints**: Minimal API in `Server/Web/Endpoints/`, grouped by feature, thin; delegate to `ISender`.
- **Logging**: Always structured (`_logger.LogX("message {Param}", param)`). Never use string interpolation (`$""`).
- **Database**: EF Core multi-provider (Postgres + Sqlite). Never call `BuildServiceProvider()` during DI registration.
- **Domain entities**: Inherit `BaseEntity`, raise events via `AddDomainEvent()`.
- **Docs and tests**: update docs and tests with behavior changes; new shared UI components need a DesignSystem demo (`docs/dev/developing.md`).

## Code Style

- Use `var` everywhere.
- File-scoped namespaces (enforced).
- Private fields: `_camelCase`. No `s_` prefix for static fields.
- Explicit accessibility modifiers on all members.
- Element ordering: fields → constructors → delegates → events → properties → methods.
- Always forward `CancellationToken`. Last parameter. Public methods: `CancellationToken cancellationToken = default`.
- Prefer pattern matching (`is null`, `is not null`) over `== null`.
- Prefer `field` keyword (C# 14) over manual backing fields in properties.
- No em dashes (`-`), curly quotes, ellipsis (`…`), or other non-ASCII punctuation in code, comments, or documentation. Use plain ASCII: `-`, `...`, `"`, `'`.

## Git Conventions

- Always use [Conventional Commits](https://www.conventionalcommits.org/): `type(scope): description`.
- Types: `feat`, `fix`, `refactor`, `perf`, `style`, `docs`, `test`, `chore`, `ci`, `build`.
- Scope is optional but recommended (e.g., `feat(signalr):`, `perf(images):`).
- Description in lowercase, no period at the end.
- Never amend a pushed commit.

## References

- Architecture and conventions: [AGENTS.md](../AGENTS.md)
- Docs index: [docs/README.md](../docs/README.md)
- Setup and contributing: [CONTRIBUTING.md](../CONTRIBUTING.md)
- Design: [docs/dev/design.md](../docs/dev/design.md)
- Code formatting: [.editorconfig](../.editorconfig)

Path-scoped Copilot files under `.github/instructions/` only point at these docs (keep `applyTo` for auto-attach).
