# K7 - Agent Instructions

Consolidated from `.github/copilot-instructions.md`, layer instructions, skills, and `.impeccable.md`.
Source of truth for Copilot remains in `.github/`; this file is the Cursor equivalent.

## Project

K7 is a self-hosted media server (music, movies, TV shows). .NET 10, C# 14, `TreatWarningsAsErrors`, nullable enabled, implicit usings, file-scoped namespaces.

## Architecture

Clean Architecture with strict dependency direction: **Domain -> Application -> Infrastructure -> Web (host)**.

| Project | Namespace | Role | May reference |
|---|---|---|---|
| `Server/Domain` | `K7.Server.Domain` | Entities, value objects, enums, events, interfaces | Nothing |
| `Server/Application` | `K7.Server.Application` | Use cases (CQRS), MediatR handlers, FluentValidation | Domain |
| `Server/Infrastructure/*` | `K7.Server.Infrastructure.*` | EF Core (Postgres + Sqlite), file system, media processing | Domain, Application |
| `Server/Web` | `K7.Server.Web` | ASP.NET Core host, Minimal API endpoints, SignalR hub | All server layers |
| `Shared/K7.Shared` | `K7.Shared` | DTOs, constants shared across client and server | Domain (enums + `nameof` only) |
| `Clients/Shared/*` | `K7.Clients.Shared.*` | Blazor components, pages, services, models | K7.Shared |
| `Clients/Web` | `K7.Clients.Web` | Blazor WebAssembly host | Clients.Shared.* |
| `Clients/MAUI` | `K7.Clients.MAUI` | .NET MAUI Blazor Hybrid host | Clients.Shared.* |

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

Pipeline behaviors (in order): Validation -> Authorization -> UnhandledException -> Performance.

## Error Handling

Exception-based. Handlers throw typed exceptions; `CustomExceptionHandler` maps them to `ProblemDetails`:
- `ValidationException` -> 400
- `ForbiddenAccessException` -> 403
- `NotFoundException` -> 404

No `Result<T>` wrapper pattern.

## Key Patterns

- **DI**: Each layer has a `DependencyInjection.cs` with `AddXxxServices()` extension methods.
- **DTO mapping**: Extension methods in `Application/Common/Mappings/` (e.g., `entity.ToLibraryDto()`). No AutoMapper.
- **Endpoints**: Minimal API in `Server/Web/Endpoints/`, grouped by feature, thin; delegate to `ISender`.
- **Database**: EF Core multi-provider (Postgres + Sqlite). Never call `BuildServiceProvider()` during DI registration.
- **Domain entities**: Inherit `BaseEntity`, raise events via `AddDomainEvent()`.

## Domain Layer (`src/Server/Domain/**`)

Zero dependencies. Entities inherit `BaseEntity`. Events inherit `BaseEvent`, raised via `AddDomainEvent()`, dispatched by EF Core interceptor.
Structure: `Common/`, `Constants/`, `Entities/`, `Enums/`, `Events/`, `Exceptions/`, `Interfaces/`, `ValueObjects/`.

## Application Layer (`src/Server/Application/**`)

- Commands: request + handler in same file, constructor injection with `private readonly` fields.
- Queries: read-only, always `AsNoTracking()`.
- Validation: FluentValidation in companion file; `ValidationBehaviour` throws `ValidationException`.
- Data access via `IApplicationDbContext`. Cross-feature dispatch via `ISender`.

## Infrastructure Layer (`src/Server/Infrastructure/**`)

- EF config: `IEntityTypeConfiguration<T>` in `Database/Context/Data/Configurations/`.
- DI: lambda-based resolution, never `BuildServiceProvider()`.
- Migrations per provider (Postgres / Sqlite) via `dotnet ef migrations add`.

## Blazor UI (`src/Clients/**`)

Shared UI in `Clients/Shared/` (Components, Pages, Services, Models). Both Web WASM and MAUI reference these.

**Triad pattern**: `MyComponent.razor` + `.razor.cs` (partial class) + optional `.razor.css`.

Placement:
- Pages -> `Clients/Shared/Pages/`
- Reusable components -> `Clients/Shared/Components/` or `Clients/Shared/UI/Components/`
- Client services -> `Clients/Shared/Services/`
- MAUI-only code -> `Clients/MAUI/`

Use K7 component library (`Clients/Shared/UI/Components/`). No third-party UI frameworks in pages.
Theming via `ThemeService` / `Themes.cs` and CSS variables. No hardcoded colors. No inline styles.
Scoped CSS preferred. Mandatory localization: no hardcoded user-facing strings; use `IStringLocalizer` + `.resx`. Default `*.resx` files are French - use proper diacritics in `<value>` strings; English goes in `*.en.resx`. Resource keys stay ASCII.

## Testing (`tests/**`)

Stack: NUnit, FluentAssertions, NSubstitute. AAA structure.
Naming: `{ClassUnderTest}Tests`, `{Method}_Should{Expected}_When{Condition}`.
Projects: `Domain.UnitTests`, `Application.UnitTests`, `Application.FunctionalTests`, `Infrastructure.IntegrationTests`.
Functional tests use `CustomWebApplicationFactory`. Integration tests use Testcontainers + Respawn.
Critical Blazor components: bUnit tests.

## Design Context

### Users
Self-hosters and their family and friends - a small, trusted circle sharing a private media server. Usage spans desktop, mobile, and couch/TV (10-foot UI).

### Brand Personality
**Cinematic. Yours. Understated.** Personal cinema - intimate, private, artisanal. Premium polish without corporate coldness.
Anti-reference: neon aesthetics, glowing cyan/purple accents, anything competing with content.

### Aesthetic Direction
- Dark-first, cinema-coded: deep blue-gray backgrounds (`#0c1018`-`#131821`).
- Content is the hero: artwork carries visual weight; UI chrome recedes.
- Extracted color from content, not imposed palettes.
- Light mode is first-class: cool off-whites, soft surfaces.
- Theme-ready tokens: copper primary (`#CC7A3E`); all colors via CSS variables, never hard-coded hex.
- Typography: Epilogue (headings), Manrope (body). No monospace as a "technical" shortcut.

### Design Principles
1. **Content first, chrome last**
2. **Cinematic restraint** - no neon, glow, or decorative gradients
3. **Every context is first-class** - desktop, mobile, couch mode
4. **Tokens over hard-codes, always**
5. **Accessible by default** - WCAG AA minimum

### UI Anti-Patterns (avoid AI slop)
- Cyan-on-dark, purple gradients, neon accents, glassmorphism, gradient text
- Hero metric layouts, identical card grids, nested cards
- Generic fonts (Inter, Roboto, Arial), bounce/elastic easing
- Gray text on colored backgrounds; pure black/white
- Hiding core functionality on mobile; shrinking desktop layout instead of adapting

## Git Conventions

Conventional Commits: `type(scope): description`. Types: `feat`, `fix`, `refactor`, `perf`, `style`, `docs`, `test`, `chore`, `ci`, `build`.
Description in lowercase, no period at end. Subject line only - no commit body or multi-line description.
Never amend a pushed commit.

## Common Commands

```bash
dotnet run --project src/Shared/Aspire/AppHost   # Dev (Postgres, pgAdmin, dashboard)
dotnet build -tl
dotnet test
dotnet format
```

## Agent Skills

### Design (project)

- `.github/skills/*/SKILL.md` - Design workflow skills (audit, adapt, critique, distill, normalize, polish)

### .NET (official)

K7 vendors [dotnet/skills](https://github.com/dotnet/skills) as a git submodule at `.github/dotnet-skills/`. Cursor loads these plugins on workspace open via `.cursor/hooks.json`:

| Plugin | Use in K7 |
|---|---|
| `dotnet-blazor` | Shared Blazor UI (Web WASM + MAUI hybrid) |
| `dotnet-aspnetcore` | Minimal API, SignalR, server host |
| `dotnet-data` | EF Core (Postgres + Sqlite) |
| `dotnet-maui` | MAUI Blazor Hybrid client |
| `dotnet-test` | NUnit / functional / integration tests |
| `dotnet-msbuild` | Build and CI troubleshooting |
| `dotnet-upgrade` | Framework and language migrations |
| `dotnet`, `dotnet-nuget`, `dotnet-diag` | General .NET, packages, performance |

After clone, initialize the submodule:

```bash
git submodule update --init --recursive .github/dotnet-skills
```

Alternative: install plugins from the [Cursor marketplace](https://cursor.com/marketplace) (search ".NET") instead of using the submodule.

## References

- `.github/copilot-instructions.md` - Copilot entry point
- `.github/instructions/*.instructions.md` - Layer-specific rules
- `.impeccable.md` - Design context source
- `CONTRIBUTING.md` - Setup and contributing
