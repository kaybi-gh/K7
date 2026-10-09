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
5. Retarget via Settings -> My device -> disconnect, or clear the preference.

Android emulator often needs `http://10.0.2.2:PORT` instead of `localhost`. Physical devices need the host LAN IP. Mac Catalyst builds are untested by the maintainer. iOS device builds are compiled in CI (`maui-ios-smoke`) and the sideload IPA is produced by [client-release](releasing.md).

Native video chrome on Android/iOS/Windows/Linux is documented in [video-playback.md](video-playback.md). When `MauiNativeVideoChrome.IsEnabledFor` is true, the host shows `NativeVideoPlayerOverlay` above ExoPlayer (Android), MediaElement (iOS), LibVLC (Windows and Linux Direct Play), or Video.js in the WebView (Windows HLS in WebView2, Linux HLS in WebKitGTK) instead of the Blazor HUD. Web WASM stays on Video.js + full Blazor controls.

OIDC on MAUI uses `k7://callback/login` on all platforms (Windows and Linux included). The unpackaged Windows build registers that protocol under HKCU at startup (no admin). The Linux build writes `~/.local/share/applications/com.k7.maui.desktop` with `MimeType=x-scheme-handler/k7` and runs `xdg-mime default` (per user, no root). After Windows or Linux system-browser sign-in the server sends the tab to `/auth/complete` (close message) then that page opens `k7://`. Android/iOS keep the direct custom-scheme 302. `http://localhost/` remains accepted by the server for older clients. On Linux, `K7_AUTH_REDIRECT=loopback` switches the authorize request to the OpenIddict loopback listener for sandboxed browsers that cannot launch host applications for custom schemes. Register compatible URIs at your IdP when testing SSO.

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

### Linux desktop (GTK4)

Experimental, built on the [dotnet/maui-labs Linux.Gtk4](https://github.com/dotnet/maui-labs/tree/main/platforms/Linux.Gtk4) backend (`Microsoft.Maui.Platforms.Linux.Gtk4*` preview packages). It is the plain `net10.0` TFM of the MAUI project: no custom TFM and no MAUI workload (the MAUI SDK is not imported for that TFM). `Platforms/Linux` holds the GTK entry point, the `k7://` registration and platform services. Helpers the Windows smoke tests cover live in `src/Clients/MAUI/Linux`. On a Linux host the project defaults to `net10.0`. Elsewhere opt in with `K7PublishPlatform=linux`.

Runtime requirements: GTK 4.12+ and WebKitGTK 6.x (`libgtk-4-1`, `libwebkitgtk-6.0-4` on Debian/Ubuntu, `gtk4` and `webkitgtk6.0` on Fedora), `xdg-utils` for the scheme handler, and a VLC 4 libvlc for Direct Play (LibVLCSharp 4 alpha, same as Windows). Release builds ship libvlc 4 next to the app in `libvlc/linux-x64` (produced by `tools/linux/bundle-libvlc.sh`, see [releasing.md](releasing.md#linux-libvlc-bundle)). A source checkout has no bundle, so either run that script once on an Ubuntu 24.04 machine or install a system VLC 4. Distros ship VLC 3. `K7_LIBVLC_DIR` points at a bundle elsewhere. Restricted user namespaces (Ubuntu 24.04+, containers) may need `MAUI_WEBKIT_DISABLE_SANDBOX=1`.

```bash
dotnet run --project src/Clients/MAUI/K7.Clients.MAUI.csproj -f net10.0 -p:K7PublishPlatform=linux

dotnet publish src/Clients/MAUI/K7.Clients.MAUI.csproj \
  -f net10.0 \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:K7PublishPlatform=linux
```

The output entry point is `K7.Clients.MAUI`. `wwwroot/` (own assets plus `_content/` of the RCLs) is copied next to it by `K7CopyStaticWebAssetsLinux` / `K7PublishStaticWebAssetsLinux`: the GTK BlazorWebView serves files from disk. Cross-compiling from Windows works (`-r linux-x64`), so `dotnet build -p:K7PublishPlatform=linux` is a valid compile check on any host. Runtime needs a Linux desktop session.

`K7_WEBKIT_INSPECTOR=1` opens the WebKit Web Inspector. Under WSLg / RDP, WebKitGTK reports wrong `KeyboardEvent.code` values. Enter detection in `navigation.js` and the card components trusts `code` only when `key` is empty or `Unidentified`. `audioplayer.js` accepts `key` as well as `code`. `Program.Main` clones the process culture with a dot decimal separator so a French alpha `0,6` does not drop a GTK style block. The `app` scheme is registered as local, CORS-enabled and secure before the first WebView. `LinuxSessionEnvironment` fills `XDG_RUNTIME_DIR` and `PULSE_SERVER` when the shell did not. LibVLC uses `--aout=pulse`. WebKitGTK media (music, HLS) needs the GStreamer good, bad, libav, pulseaudio and gl plugins.

Direct Play is LibVLC 4 through vmem into a `Gtk.Picture`, with the native overlay. HLS is Video.js in WebKitGTK under that same overlay (the WebView stays visible and loses pointer targeting). Music is audioplayer.js. MediaElement and SKLottieView have no GTK handlers and are removed at construction. The splash Lottie is Skottie into a `Gtk.Picture` (`LinuxLottieView`). See [video-playback.md](video-playback.md#linux-libvlc-direct-play-videojs-hls-transcode).

`k7://` on Linux: the browser launches a second process with the URI. The primary holds `$XDG_RUNTIME_DIR/k7-maui-primary.lock`. A second process never boots GTK. It forwards the URI over `$XDG_RUNTIME_DIR/k7-maui-protocol.sock`, else the callback file, then exits. Release assets: `K7-{version}-linux-x64.deb` and `K7-{version}-linux-x64.tar.gz`. See [releasing.md](releasing.md).

iOS device (macOS host, single TFM via `K7PublishPlatform`):

```bash
dotnet build src/Clients/MAUI/K7.Clients.MAUI.csproj \
  -c Release \
  -f net10.0-ios \
  -p:K7PublishPlatform=ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:RunAOTCompilation=false \
  -p:UseInterpreter=true \
  -p:MtouchLink=None \
  -p:EnableCodeSigning=false
```

Release CI applies extra sideload packaging (ad-hoc IPA, AltStore `apps.json`). See [releasing.md](releasing.md) and [`altstore/README.md`](../../altstore/README.md).

Published Release assets (APK, Windows zip, Linux deb and tarball, iOS sideload IPA) are produced by [client-release](releasing.md) on each GitHub Release.

Android TV: leanback launcher category is registered - use a TV emulator for D-pad testing. Fire TV Stick uses the same APK (leanback / Fire TV feature / AFT model, not UiMode alone). Couch layout stays near 1920 CSS px so a 4K framebuffer does not shrink the 10-foot UI.

Shared UI placement: [architecture.md](architecture.md#ui-layout).

### MAUI startup

Typical sequence for a returning multi-user device: Android DecorView shows `k7_logo`, starts a looping Skottie blit when ready, and builds `BlazorPage` immediately under that overlay. First paint of `/select-profile` (EmptyLayout, local users and pinned SharedProfiles from device storage) dismisses the overlay. The picker does not call SharedProfiles. Solo auto-login applies `BackendUrl` first, then restores the session, starts at `/`, and dismisses on MainLayout first paint. A `BlazorPage` construction failure keeps the stored server URL (it must not dump the user onto native setup). First-run TV with Guest disabled starts at `/linkdevice`. Player scripts (`video.min.js`, audioplayer) load after first paint on Windows / Web, and are awaited if play happens before the prefetch finishes.

Android `MainActivity` ignores restored instance state so a TV/process death cannot paint a frozen Blazor snapshot (visible select-profile, dead remote). `OnResume` resumes WebView timers, puts focus back on the WebView, dismisses leftover splash overlays, and re-inits spatial nav. `OnWindowFocusChanged` retries that focus once the window can take it. D-pad is forwarded into the page when the WebView does not own focus. The activity is recreated if the JS bridge or the Blazor dispatcher does not answer. On TV the WebView renderer is kept while the screen is off so standby cannot leave a frozen frame. `AppLifecycleGate` suppresses music UI renders while the host is paused so the mini player does not replay every track change when the screen turns back on.

On Android the splash is attached to the activity DecorView so it stays above WebView / MediaElement and survives `BlazorPage` construction. Playback uses Skottie rendered into an `ImageView` (not `SKCanvasView` and not Airbnb Lottie): after the SkiaSharp 4 bump, `SKCanvasView.PaintSurface` often never fires on Android TV, while Skottie itself still renders to a bitmap. The overlay is shown immediately in `OnCreate` (static `k7_logo`). When Skottie is ready it loops and `BlazorPage` starts right away under it. The overlay stays until Blazor dismisses. Windows / iOS keep `SKLottieView` on the Blazor overlay. The Android 12+ system splash icon is always a circle, so `MauiSplashScreen` is brand color only (`#0d0907`).

## DesignSystem

`src/Clients/DesignSystem` is a **Blazor Server catalog** of the shared UI library (branding, tokens, components, players, dialogs, layout). It uses mock services - no K7 server required.

Pre-colored logo and symbol SVGs (the Branding page variants) live in [`branding/`](../../branding/).

```bash
dotnet run --project src/Clients/DesignSystem
# or via Aspire (service k7-design-system)
dotnet run --project src/Shared/Aspire/AppHost
```

Standalone URL: see `src/Clients/DesignSystem/Properties/launchSettings.json` (typically `https://localhost:61567`). Use the Dark / Light toggle in the catalog sidebar to check contrast.

### Adding or changing a shared component

1. Implement in `src/Clients/Shared/UI/Components/` (or `Dialogs/`, `Players/`) with the triad + localization. `K7GroupedList` is the searchable grouped catalog (notification parameters, rule fields).
2. Add a demo section on the matching DesignSystem page (`Pages/Components.razor`, `Players.razor`, `Dialogs.razor`, ... ) with a stable `id`.
3. If the type name starts with `K7`, add it to the `demoed` set in `Pages/Index.razor.cs` (home page lists uncatalogued `K7*` types via reflection).
4. Add a sidebar anchor in `Layout/DesignLayout.razor`.
5. If the component needs services, add a mock in `Mocks/MockServices.cs` and register it in `Program.cs`.
6. Run DesignSystem and confirm the home page no longer flags the component as missing.
7. Extend `Clients.DesignSystem.SmokeTests` only if new host DI is required for startup.

Visual rules and the public theme contract: [design.md](design.md).

## Localization

- Default `.resx` files are **French** (proper diacritics in values)
- English in `*.en.resx`
- Resource **keys** stay ASCII
- No hardcoded user-facing strings - use `IStringLocalizer`

Supported interface languages: `src/Shared/K7.Shared/SupportedLanguages.cs` (`fr`, `en`). Resources under `src/Clients/Shared/UI/Resources/...` and some under `src/Server/Web/Resources/`.

**Adding a string:** French default `.resx` -> English `*.en.resx` -> inject localizer -> spot-check both cultures.

**Adding a language:** extend `SupportedLanguages` and request localization registration; add `*.xx.resx` siblings.

## API (OpenAPI)

K7 generates an **OpenAPI 3.1** document for the server HTTP API.

| Item | Detail |
|---|---|
| Build output | `src/Server/Web/wwwroot/openapi/specification.json` (generated into `obj/` then copied, `OpenApiGenerateDocumentsOnBuild`) |
| Runtime static spec | `/openapi/specification.json` |
| Scalar UI | `/scalar` - **Development only** |

A normal `dotnet build` on `src/Server/Web` regenerates the document. Generation writes under `obj/` then copies into `wwwroot`. If the host still has the file mapped (Aspire debug restart) that copy is skipped and the previous spec stays until the next unlocked build. Prefer shared DTOs in `K7.Shared` for first-party clients. Automation uses API keys via `X-Api-Key` (native API) or OpenSubsonic `apiKey` on `/rest` - see [Configuration - Security](../admin/configuration.md#hardening-checklist). OpenSubsonic facade: [Architecture](architecture.md#opensubsonic-compatibility-layer).

## Testing

Stack: **NUnit**, **AwesomeAssertions**, **NSubstitute**. Blazor component tests use **bUnit**. Naming: `{ClassUnderTest}Tests`, `{Method}_Should{Expected}_When{Condition}`.

New behavior should ship with tests in the matching project (unit, bUnit, functional, or integration). Prefer covering the happy path and important failure cases for Application handlers and critical UI.

### Test projects

| Project | What | CI |
|---|---|---|
| `Domain.UnitTests` / `Application.UnitTests` / `Import.UnitTests` | Unit | `build.yml` (fast) |
| `Clients.ComponentTests` | bUnit | fast |
| `Web.SmokeTests` / `Clients.DesignSystem.SmokeTests` | Smoke | fast |
| `Clients.MAUI.SmokeTests` | MAUI smoke | `build.yml` `maui-smoke` (Windows, after `build-and-test`) |
| MAUI iOS compile | Sideload-shaped `net10.0-ios` build | `build.yml` `maui-ios-smoke` (macOS, after `build-and-test`) |
| `Application.FunctionalTests` / `Infrastructure.IntegrationTests` | HTTP + EF | `build.yml` `integration` (after `build-and-test`) |
| `Tests.Helpers` | Factories, Testcontainers | referenced |

[`K7.CI.slnf`](../../K7.CI.slnf) is the **fast CI** filter (excludes MAUI, Aspire AppHost, functional and integration tests).

```bash
dotnet test
dotnet test tests/Application.UnitTests/Application.UnitTests.csproj
dotnet test --filter "FullyQualifiedName~CreateLibrary"
dotnet test K7.CI.slnf
```

Functional/integration tests need **Docker** (Testcontainers.PostgreSQL + Respawn). Without Docker, unit and bUnit projects still run.

`build.yml` runs `build-and-test` first (restore, vulnerable-package check, Release build of `K7.CI.slnf`, fast tests). `maui-smoke`, `maui-ios-smoke`, integration tests, CodeQL, and `publish-image` start only if that job succeeds. CodeQL still has a weekly schedule (and a manual `workflow_dispatch`) in [`codeql.yml`](../../.github/workflows/codeql.yml).

If branch protection requires status checks, use the names under the **Build** workflow (`build-and-test`, `maui-smoke`, `maui-ios-smoke`, `Integration tests / integration`, `CodeQL / Analyze (csharp)`, `publish-image`). The old standalone **Integration Tests** and **CodeQL** PR checks no longer run on push/PR.

## Dependency updates

K7 uses **[Renovate](https://docs.renovatebot.com/)** (self-hosted via GitHub Actions), not Dependabot version updates. Config: [`renovate.json`](../../renovate.json). Workflow: [`.github/workflows/renovate.yml`](../../.github/workflows/renovate.yml) (weekly Monday + `workflow_dispatch`).

Renovate groups only packages that must bump together (OpenIddict, OpenTelemetry, SkiaSharp, LibVLC, SQLite natives, Google Play Services, Microsoft runtime minors including `dotnet-ef`, Microsoft MAUI). The Microsoft 10.0.x runtime group is intentional: with `CentralPackageTransitivePinningEnabled`, splitting EF Core from Extensions/AspNetCore causes NU1109. MAUI stays in its own group (`10.0.100`-style versions). Everything else gets its own PR so one breaking bump cannot block the rest. Majors for `Microsoft.OpenApi` (incompatible with `Microsoft.AspNetCore.OpenApi` 10) are disabled.

`Directory.Packages.props` also pins some transitive packages (`Azure.Identity`, `System.Drawing.Common`, `SSH.NET`) to patched versions. CI fails `dotnet list package --vulnerable` if those pins are removed.

Commit messages use `chore(deps): bump <package> to vX.Y.Z`. Renovate always writes **one commit per PR** (it force-pushes that single commit when it rebases). Isolated per-package history therefore comes from ungrouped PRs, not from multiple commits inside a grouped PR.

The job installs the `maui-android` workload on the runner so NuGet restore works for MAUI, including `android-arm` (Fire Stick / 32-bit). It uses the workflow `GITHUB_TOKEN` (no secret required).

Repo **Settings -> Actions -> General** must allow Actions to create pull requests (write permissions). Without this, Renovate pushes branches but cannot open PRs.

GitHub does not start workflows from events created by `GITHUB_TOKEN` (push, `pull_request`, or `pull_request_target`). After Renovate opens or updates PRs, the Renovate job dispatches **Build** on any `renovate/*` head that still has no check runs (integration tests and CodeQL are jobs in that workflow). `workflow_dispatch` and `repository_dispatch` are the exceptions GitHub allows with `GITHUB_TOKEN`.

Run **Actions -> Renovate -> Run workflow** once to verify after changing Renovate config.

You can still enable **Dependabot alerts** (security advisories) in GitHub Settings without Dependabot version-update PRs.

Close or merge any leftover open Dependabot PRs so they do not compete with Renovate.
