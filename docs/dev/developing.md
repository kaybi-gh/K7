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

Launch profiles: `src/Server/Web/Properties/launchSettings.json`. Typical HTTPS URL: `https://localhost:7443` (HTTP: `http://localhost:7080`). There is no supported standalone "WASM only against remote API" profile in-repo.

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

Native video chrome on Android/iOS is documented in [video-playback.md](video-playback.md). When `MauiNativeVideoChrome.IsEnabled` is true (non-Windows MAUI), the host shows `NativeVideoPlayerOverlay` above MediaElement instead of the Blazor HUD. Windows MAUI stays on Video.js + full Blazor controls.

OIDC on MAUI includes `http://localhost/` redirect URIs - register compatible URIs at your IdP when testing SSO.

Android (single TFM via `K7PublishPlatform`; do not pass global `-p:TargetFrameworks=`):

```bash
dotnet publish src/Clients/MAUI/K7.Clients.MAUI.csproj \
  -f net10.0-android \
  -c Release \
  -p:K7PublishPlatform=android
```

Windows unpackaged (self-contained):

```bash
dotnet publish src/Clients/MAUI/K7.Clients.MAUI.csproj \
  -f net10.0-windows10.0.19041.0 \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:K7PublishPlatform=windows \
  -p:UseMonoRuntime=false \
  -p:WindowsPackageType=None
```

Output entry point is `K7.Clients.MAUI.exe` (plus `K7.Clients.MAUI.pri`). Release CI also copies those to `K7.exe` / `K7.pri` for a shorter launcher name - WinUI requires the `.pri` basename to match the `.exe`. Do not ship a renamed exe without the matching `.pri`.

Published Release assets (APK + Windows zip) are produced by [client-release](releasing.md#android-signing) on each GitHub Release.

Android TV: leanback launcher category is registered - use a TV emulator for D-pad testing.

Shared UI placement: [architecture.md](architecture.md#ui-layout).

### MAUI startup

Typical sequence for a returning multi-user device: Android DecorView Lottie plays once and holds, then `BlazorPage` is constructed under the overlay, then first paint of `/select-profile` (EmptyLayout) dismisses the overlay. Solo auto-login applies `BackendUrl` first, then restores the session, starts at `/`, and dismisses on MainLayout first paint. A `BlazorPage` construction failure keeps the stored server URL (it must not dump the user onto native setup). First-run TV with Guest disabled starts at `/linkdevice`. Player scripts (`video.min.js`, audioplayer) load after first paint on Windows / Web, and are awaited if play happens before the prefetch finishes.

On Android the Lottie is attached to the activity DecorView so it stays above WebView / MediaElement and survives `BlazorPage` construction. Windows / iOS keep `SKLottieView` on the Blazor overlay.

## DesignSystem

`src/Clients/DesignSystem` is a **Blazor Server catalog** of the shared UI library (branding, tokens, components, players, dialogs, layout). It uses mock services - no K7 server required.

Pre-colored logo and symbol SVGs (the Branding page variants) live in [`branding/`](../../branding/).

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

A normal `dotnet build` on `src/Server/Web` regenerates the document. Prefer shared DTOs in `K7.Shared` for first-party clients. Automation uses API keys via `X-Api-Key` (native API) or OpenSubsonic `apiKey` on `/rest` - see [Configuration - Security](../admin/configuration.md#hardening-checklist). OpenSubsonic facade: [Architecture](architecture.md#opensubsonic-compatibility-layer).

## Testing

Stack: **NUnit**, **FluentAssertions**, **NSubstitute**. Blazor component tests use **bUnit**. Naming: `{ClassUnderTest}Tests`, `{Method}_Should{Expected}_When{Condition}`.

New behavior should ship with tests in the matching project (unit, bUnit, functional, or integration). Prefer covering the happy path and important failure cases for Application handlers and critical UI.

### Test projects

| Project | What | CI |
|---|---|---|
| `Domain.UnitTests` / `Application.UnitTests` / `Import.UnitTests` | Unit | `build.yml` (fast) |
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

Functional/integration tests need **Docker** (Testcontainers.PostgreSQL + Respawn). Without Docker, unit and bUnit projects still run.

## Dependency updates

K7 uses **[Renovate](https://docs.renovatebot.com/)** (self-hosted via GitHub Actions), not Dependabot version updates. Config: [`renovate.json`](../../renovate.json). Workflow: [`.github/workflows/renovate.yml`](../../.github/workflows/renovate.yml) (weekly Monday + `workflow_dispatch`).

The job installs the `maui-android` workload on the runner so NuGet restore works for MAUI, including `android-arm` (Fire Stick / 32-bit). It uses the workflow `GITHUB_TOKEN` (no secret required).

Repo **Settings -> Actions -> General** must allow Actions to create pull requests (write permissions). Without this, Renovate pushes branches but cannot open PRs.

`pull_request` CI does not run on PRs opened by `github-actions[bot]`. Build and integration-tests workflows also listen on `pull_request_target` for bot PRs (Renovate, Dependabot).

Run **Actions -> Renovate -> Run workflow** once to verify after changing Renovate config.

You can still enable **Dependabot alerts** (security advisories) in GitHub Settings without Dependabot version-update PRs.

Close or merge any leftover open Dependabot PRs so they do not compete with Renovate.
