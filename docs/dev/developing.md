# Developing

Day-to-day development. Architecture: [architecture.md](architecture.md). Setup and PRs: [CONTRIBUTING.md](../../CONTRIBUTING.md).

When you add or change a feature, also update **tests** and **documentation** (user / admin / `docs/dev` as relevant). See [CONTRIBUTING - Pull requests](../../CONTRIBUTING.md#pull-requests).

## Clients (Web + MAUI)

### Web (Blazor WASM)

`K7.Clients.Web` is hosted by `K7.Server.Web`. The WASM `HttpClient` uses `HostEnvironment.BaseAddress` (same origin).

```bash
dotnet run --project src/Shared/Aspire/AppHost
# or
dotnet run --project src/Server/Web
```

Launch profiles: `src/Server/Web/Properties/launchSettings.json`. Typical HTTPS URL: `https://localhost:5001`. There is no supported standalone "WASM only against remote API" profile in-repo.

### MAUI (Blazor Hybrid)

Project: `src/Clients/MAUI`.

```bash
dotnet workload install maui
```

1. Start the server and note a URL reachable from the emulator/device.
2. Launch MAUI for the desired TFM.
3. On first launch, enter the server URL; the app probes `{url}/health` and stores preference `BackendUrl` (`K7_SERVER_URL`).
4. After first URL setup the app **closes** (known limitation) - reopen it, then sign in.
5. Retarget via Settings -> General -> disconnect, or clear the preference.

Android emulator often needs `http://10.0.2.2:PORT` instead of `localhost`. Physical devices need the host LAN IP. iOS/Mac builds are untested by the maintainer.

OIDC on MAUI includes `http://localhost/` redirect URIs - register compatible URIs at your IdP when testing SSO.

```bash
dotnet publish src/Clients/MAUI/K7.Clients.MAUI.csproj \
  -f net10.0-android \
  -c Release
```

Windows unpackaged (self-contained):

```bash
dotnet publish src/Clients/MAUI/K7.Clients.MAUI.csproj \
  -f net10.0-windows10.0.19041.0 \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:WindowsPackageType=None
```

Published Release assets (APK + Windows zip) are produced by [client-release](releasing.md#android-signing) on each GitHub Release.

Android TV: leanback launcher category is registered - use a TV emulator for D-pad testing.

Shared UI placement: [architecture.md](architecture.md#ui-layout).

## DesignSystem

`src/Clients/DesignSystem` is a **Blazor Server catalog** of the shared UI library (branding, tokens, components, players, dialogs, layout). It uses mock services - no K7 server required.

```bash
dotnet run --project src/Clients/DesignSystem
# or via Aspire (service k7-design-system)
dotnet run --project src/Shared/Aspire/AppHost
```

Standalone URL: see `src/Clients/DesignSystem/Properties/launchSettings.json` (typically `https://localhost:61567`).

### Adding or changing a shared component

1. Implement in `src/Clients/Shared/UI/Components/` (or `Dialogs/`, `Players/`) with the triad + localization.
2. Add a demo section on the matching DesignSystem page (`Pages/Components.razor`, `Players.razor`, `Dialogs.razor`, ... ) with a stable `id`.
3. If the type name starts with `K7`, add it to the `demoed` set in `Pages/Index.razor.cs` (home page lists uncatalogued `K7*` types via reflection).
4. Add a sidebar anchor in `Layout/DesignLayout.razor`.
5. If the component needs services, add a mock in `Mocks/MockServices.cs` and register it in `Program.cs`.
6. Run DesignSystem and confirm the home page no longer flags the component as missing.
7. Extend `Clients.DesignSystem.SmokeTests` only if new host DI is required for startup.

Visual rules: [design.md](design.md).

## Localization

- Default `.resx` files are **French** (proper diacritics in values)
- English in `*.en.resx`
- Resource **keys** stay ASCII
- No hardcoded user-facing strings - use `IStringLocalizer`

Supported interface languages: `src/Shared/K7.Shared/SupportedLanguages.cs` (`fr`, `en`). Resources under `src/Clients/Shared/UI/Resources/...` and some under `src/Server/Web/Resources/`.

**Adding a string:** French default `.resx` -> English `*.en.resx` -> inject localizer -> spot-check both cultures.

**Adding a language:** extend `SupportedLanguages` and request localization registration; add `*.xx.resx` siblings.

Accent / mojibake helpers may live under `scripts/` when present; otherwise edit `.resx` in the IDE.

## API (OpenAPI)

K7 generates an **OpenAPI 3.1** document for the server HTTP API.

| Item | Detail |
|---|---|
| Build output | `src/Server/Web/wwwroot/openapi/specification.json` (`OpenApiGenerateDocumentsOnBuild`) |
| Runtime static spec | `/openapi/specification.json` |
| Scalar UI | `/scalar` - **Development only** |

A normal `dotnet build` on `src/Server/Web` regenerates the document. Prefer shared DTOs in `K7.Shared` for first-party clients. Automation: Admin API keys via `X-Api-Key` - [Configuration - Security](../admin/configuration.md#hardening-checklist).

## Testing

Stack: **NUnit**, **FluentAssertions**, **NSubstitute**. Blazor component tests use **bUnit**. Naming: `{ClassUnderTest}Tests`, `{Method}_Should{Expected}_When{Condition}`.

New behavior should ship with tests in the matching project (unit, bUnit, functional, or integration). Prefer covering the happy path and important failure cases for Application handlers and critical UI.

### Test projects

| Project | What | CI |
|---|---|---|
| `Domain.UnitTests` / `Application.UnitTests` | Unit | `build.yml` (fast) |
| `Clients.ComponentTests` | bUnit | fast |
| `Web.SmokeTests` / `Clients.DesignSystem.SmokeTests` | Smoke | fast |
| `Clients.MAUI.SmokeTests` | MAUI smoke | `maui-smoke` (Windows) |
| `Application.FunctionalTests` / `Infrastructure.IntegrationTests` | HTTP + EF | `integration-tests.yml` |
| `Tests.Helpers` | Factories, Testcontainers | referenced |

[`K7.CI.slnf`](../../K7.CI.slnf) is the **fast CI** filter (excludes MAUI, Aspire AppHost, functional and integration tests).

```bash
dotnet test
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj
dotnet test --filter "FullyQualifiedName~CreateLibrary"
dotnet test K7.CI.slnf
```

Functional/integration tests need **Docker** (Testcontainers.PostgreSql + Respawn). Without Docker, unit and bUnit projects still run.
