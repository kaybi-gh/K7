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

OIDC on MAUI uses `k7://callback/login` on all platforms (Windows and Linux included). The unpackaged Windows build registers that protocol under HKCU at startup (no admin). The Linux build writes `~/.local/share/applications/com.k7.maui.desktop` with `MimeType=x-scheme-handler/k7` and runs `xdg-mime default` (per user, no root). After native sign-in the server sends Windows / Android / iOS tabs to `/auth/complete` (close message) then that page opens `k7://`. A raw custom-scheme 302 is unreliable after local MFA in Chrome Custom Tabs. `http://localhost/` remains accepted by the server for older clients. On Linux, `K7_AUTH_REDIRECT=loopback` switches the authorize request to the OpenIddict loopback listener for sandboxed browsers that cannot launch host applications for custom schemes. Register compatible URIs at your IdP when testing SSO.

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

Runtime requirements: GTK 4.12+ and WebKitGTK 6.x (`libgtk-4-1`, `libwebkitgtk-6.0-4` on Debian/Ubuntu, `gtk4` and `webkitgtk6.0` on Fedora), `xdg-utils` for the scheme handler, and a VLC 4 libvlc for Direct Play (LibVLCSharp 4 alpha, same as Windows). Release builds ship libvlc 4 next to the app in `libvlc/linux-x64` (produced by `tools/linux/bundle-libvlc.sh`, see [releasing.md](releasing.md#linux-libvlc-bundle)). A source checkout has no bundle, so either run that script once on an Ubuntu 24.04 machine or install a system VLC 4. Distros ship VLC 3. `K7_LIBVLC_DIR` points at a bundle elsewhere. Restricted user namespaces (Ubuntu 24.04+, containers, Flatpak) may need `K7_WEBKIT_DISABLE_SANDBOX=1` (the Flatpak wrapper sets it by default).

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

`K7_WEBKIT_INSPECTOR=1` opens the WebKit Web Inspector. `K7_WEBKIT_CONSOLE=1` copies the page console (JS errors, failed fetches) to stdout instead, handy under Flatpak or remote desktop. Every HLS start on Linux also logs `web video probe hls+2s|6s|15s {...}` (and F9 logs one on demand): Video.js players, `<video>` source / readyState / error, container visibility, the stream bridge and VHS state (`bridge`, `player`: tech, `<source>` children, MediaSource and playlist loader state), the last console warnings / errors (`log`) and the HLS requests the page saw (`hlsRequests`, always 0 on Linux: VHS fetches through the .NET bridge, traced as `stream fetch ...` lines). Under WSLg / RDP, WebKitGTK reports wrong `KeyboardEvent.code` values. Enter detection in `navigation.js` and the card components trusts `code` only when `key` is empty or `Unidentified`. `audioplayer.js` accepts `key` as well as `code`. `Program.Main` clones the process culture with a dot decimal separator so a French alpha `0,6` does not drop a GTK style block. The `app` scheme is registered as local, CORS-enabled and secure before the first WebView. `LinuxSessionEnvironment` fills `XDG_RUNTIME_DIR` and `PULSE_SERVER` when the shell did not. LibVLC uses `--aout=pulse`. WebKitGTK media (music, HLS) needs the GStreamer good, bad, libav, pulseaudio and gl plugins.

Direct Play is LibVLC 4 through vmem into a `Gtk.Picture`, with the native overlay. HLS is Video.js in WebKitGTK under that same overlay (the WebView stays visible and loses pointer targeting). Music is audioplayer.js. MediaElement and SKLottieView have no GTK handlers and are removed at construction. The splash Lottie is Skottie into a `Gtk.Picture` (`LinuxLottieView`). See [video-playback.md](video-playback.md#linux-libvlc-direct-play-videojs-hls-transcode).

`k7://` on Linux: the browser launches a second process with the URI. The primary holds `$XDG_RUNTIME_DIR/k7-maui-primary.lock`. A second process never boots GTK. It forwards the URI over `$XDG_RUNTIME_DIR/k7-maui-protocol.sock`, else the callback file, then exits. Release asset: `K7-{version}-linux-x64.flatpak`, the only Linux package (the `.deb` and the tarball depended on the host GTK4 / WebKitGTK / GStreamer versions and were dropped; the publish tree still runs directly for development). Flatpak packing: [`tools/linux/build-flatpak.sh`](../../tools/linux/build-flatpak.sh) (see [releasing.md](releasing.md#linux-flatpak)). Run with `flatpak run com.k7.maui`. Flatpak exports the desktop file (scheme handler) to the host so browser sign-in works. Inside the sandbox, `FLATPAK_ID` only skips a redundant per-user desktop rewrite that would not reach the host.

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

Published Release assets (APK, Windows zip, Linux deb / tarball / Flatpak, iOS sideload IPA) are produced by [client-release](releasing.md) on each GitHub Release.

Android TV: leanback launcher category is registered - use a TV emulator for D-pad testing. Fire TV Stick uses the same APK (leanback / Fire TV feature / AFT model, not UiMode alone). Couch layout stays near 1920 CSS px so a 4K framebuffer does not shrink the 10-foot UI.

Shared UI placement: [architecture.md](architecture.md#ui-layout).

### MAUI startup

Typical sequence for a returning multi-user device: Android DecorView shows `k7_logo`, starts a looping Skottie blit when ready, and builds `BlazorPage` immediately under that overlay. First paint of `/select-profile` (EmptyLayout, local users and pinned SharedProfiles from device storage) dismisses the overlay. The picker does not call SharedProfiles. Solo auto-login applies `BackendUrl` first, then restores the session, starts at `/`, and dismisses on MainLayout first paint. `GetAuthenticationStateAsync` returns the current principal immediately (provisional access token when still valid) and does not start OpenIddict restore. `OpenIddictClientService` is resolved lazily so CascadingAuthenticationState first paint does not pay its DI cost. OpenIddict SQLite, offline DB, server registration, session restore, and playback warmups run after `FirstPaintSignal` (layout `OnAfterRender`), not after early `AppReadySignal` from page init. Headless Android Auto runs the same heavy work via `EnsureReadyAsync` then awaits restore. A `BlazorPage` construction failure keeps the stored server URL (it must not dump the user onto native setup). First-run TV with Guest disabled starts at `/linkdevice`.

**Android cold-start work (hybrid floor):**

- `MauiStartupTrace` markers (filter `adb logcat -s K7-Startup`; every line ends with `jit=methods/ms asm=N gc0=N`, the Mono JIT and assembly-load totals at that point): `env`, `CreateMauiApp-*`, `CreateWindow-*`, `SessionRestore-*`, `AssignStartPage-*`, `WebView-create-*` / `WebView-connect-*`, `BlazorWebView-initialized`, `SplashOverlay-LogoShown` (or `LogoMissing`), `SplashOverlay-TickerThrottled`, `SplashOverlay-SkottieAttached`, `splash-dismiss-ui`, `StartPage-Loaded`, `BlazorPage-Loaded`, `AuthState-first-*`, `ProvisionalToken-*`, `MauiRoutes-init` / `MauiRoutes-firstRender`, `EmptyLayout-init`, `SelectProfile-init-*`, `SelectProfile-params-set`, `AppReadySignal`, `EmptyLayout-firstRender` / `FirstPaintSignal`, `DeferredHeavy-*` (`start`, `offline-db`, `openiddict-db`, `server-url`, `auth-restore`, `end`: each step of the worker-thread heavy work), `SelectProfile-shell-painted`, `StartPageSet-notified`, `MediaLibrary-warmup-*`, `MediaLibrary-StartService` (or `MediaLibrary-start-failsafe`), `MediaLibrary-OnCreate-*` with `MediaLibrary-services-resolved`, `MediaLibrary-base-address`, `MediaLibrary-players-created`, `MediaLibrary-sessions-built` in between, `BlazorPage-Loaded-subscribed`, `NativeVideoOverlay-init-*` (native video chrome, built 1.5 s after first paint), `ServerUrl-manager-resolve-start` / `ServerUrl-manager-resolved` / `ServerUrl-registration-end` (OpenIddict options build vs registration), `OpenIddictKeys-persisted` (or `-ephemeral-fallback`), `AuthRestore-start` / `AuthRestore-refresh-start` / `AuthRefresh-token-endpoint-*` / `AuthRestore-refresh-end` / `AuthRestore-shared-profile` / `AuthRestore-end`, `AuthSession-restored-from-storage` (signed in from the stored token + claims snapshot, no token endpoint), `AccessToken-changed`, `ProvisionalToken-prefetched` (worker-thread SecureStorage read started at `StartRestore`), `Headless-*` (`ready-start`, `heavy-done`, `auth-start`, `attempt`, `auth-usable` or `auth-exhausted`, `auth-done`, `device-done`, `reporting-done`, `music-done`: the `EnsureReadyAsync` chain the media service runs on the thread pool), `Hub-ensure-start` / `Hub-connect-start` / `Hub-connected`, `MainLayout-init` / `MainLayout-firstRender` (solo auto-login lands here), `MediaLibrary-token-read` (the one SecureStorage read the service caches; the launcher's Media3 browse callbacks used to re-read it on the main thread every few seconds), `SplashOverlay-web-painted` / `splash-fade-start` / `splash-dismiss`. `js-*` markers come from MAUI `index.html` through the `K7StartupTrace` JavaScript bridge (`StartupTraceJsBridge`, Android only; `console.log` fallback elsewhere): page load (`js-head-start`, `js-head-end`, `js-dom-content-loaded`, `js-window-load`), Blazor IPC (`js-blazor-start`, `js-first-message-to-dotnet`, `js-first-render-batch-received`, `js-first-dom-mutation`, `js-first-render-completed-sent`), `js-dismiss-preload`, script loading (`js-critical-scripts-loaded`, `js-deferred-scripts-*`). `MainThread-stall#n Xms` lines come from a 90 s watchdog thread (post latency to the main looper above 250 ms). `Mark` logs a name once; markers that end in `#n` come from `MarkRepeat` and log every occurrence (token refreshes, restore attempts) - the one-shot form hid four extra refresh grants per cold start. Stored session: older servers issue encrypted access tokens (OpenIddict JWEs - the server now signs plain JWTs with `DisableAccessTokenEncryption`), so the client cannot rely on reading `exp` or claims from them (`JwtSecurityTokenHandler` returns `DateTime.MinValue` and no claims without throwing). `CustomAuthenticationStateProvider` therefore stores the expiry from the token response (`AccessTokenExpiresAt`, Preferences) and a claims snapshot (`SessionClaims`, SecureStorage) next to the token, and `StoredSession` (shared helper) decides "still usable" (1 min skew) and "near expiry" (10 min, `AuthSessionKeeper`). While the token is usable, cold start signs in from storage (provisional token at first paint, no OpenIddict or network), the headless loop stops at its first check, and the keeper only refreshes near expiry; the snapshot is removed with the access token (sign-out, user switch, invalid grant). Android DecorView splash (logo + Skottie, throttled to ~10fps once BlazorWebView is initialized) stays until the page has painted: index.html wraps `K7.dismissPreload`, the `K7StartupTrace` bridge receives `dismiss-preload` and fades the overlay out (350 ms) over the page while the in-page preload fades; `BlazorPage` keeps a fallback at `FirstPaintSignal` + 400 ms and a 20 s failsafe. Dismissing at WebView init used to leave ~2 s of dark WebView between the mark and select-profile on TV. The OpenIddict client uses persisted symmetric keys on Android/iOS (`OpenIddictClientKeys`: HS256 + A256KW, 256-bit, SecureStorage, reset with the server, registered through `options.Configure` so the storage read happens when the options are built on the DeferredHeavy worker and not inside `CreateMauiApp`); they only protect the state tokens of interactive logins, and ephemeral RSA keys cost two RSA-2048 generations per launch (10 s of the DeferredHeavy path on a 32-bit TV box). The refresh grant itself runs on the thread pool (`Task.Run` in `TryRefreshTokenAsync`): its first run JITs the OpenIddict pipeline and must not stall the Blazor dispatcher during the solo auto-login. `AppReady` at SelectProfile init (no JS await). `FirstPaintSignal` from layout `OnAfterRender` starts DeferredHeavy - not on early `AppReady` (that starved shell ~13s). DeferredHeavy is one shared worker-thread task (`MauiSessionBootstrap.EnsureHeavyWorkAsync`); headless `EnsureReadyAsync` awaits it and nothing runs it inline on the main thread. `MainActivity` starts `K7MediaLibraryService` 2 s after `FirstPaintSignal` (30 s failsafe) after warming its DI graph on a worker thread (`WarmUpDependencies`); the service's `OnCreate` sets only the HTTP base address. It used to start at `StartPageSet` and call `ApplyServerUrl` (OpenIddict options + ephemeral RSA keys) on the main thread: 24 s stall before first paint and an ANR on a 32-bit TV box.
- `BlazorPage.xaml` is WebView + splash logo only. `MediaElement` / `SKLottieView` are created in code. On Android, video MediaElement/Exo attaches on first play (DecorView Skottie covers splash). Markers: `InitializeComponent-*`, `CreatePlatformChrome-*`, `AssignStartPage-begin`. Android Auto keeps its own Exo in `K7MediaLibraryService`.
- MAUI `index.html` keeps theme / Blazor / spatial-nav / `navigation.js` early on every device (`navigation.js` is the shell JS API: menu anchors, music flush hook, mobile locks; MainLayout's first render calls it, and the solo auto-login reaches home faster than a deferred load). Embla, QR and `playbackAssets.js` load after AppReady (4s failsafe). Carousel / QR / players call `K7.ensureEmbla` / `ensureQrCode` / `ensurePlaybackAssets` on demand.
- Release Android: `RunAOTCompilation` + `AndroidEnableProfiledAot` (profiled Mono AOT, `TrimMode=partial`, not NativeAOT: EF Core, OpenIddict and Blazor Hybrid keep the JIT fallback for dynamic code) with the default `maui` / `maui-blazor` profiles plus `src/Clients/MAUI/custom.aprof`, recorded on the 32-bit TV box (40.8k methods, 20k of them generic instantiations; solo home, movie page, playback, music). The default profiles alone only covered the template startup path: K7 pages, SignalR and the auth stack still JITted 6-7 s before home (capture 6, `jit=` totals in the markers). `-p:AndroidEnableProfiledAot=false` builds the full-AOT variant for an A/B measurement against the same `K7-Startup` markers. Dynamic code (System.Text.Json reflection emit, compiled expressions) still JITs, which is why `AndroidStripILAfterAOT` stays off. Measured 2026-10-28 on the 32-bit TV box (105 MB APK, 212 AOT images, 91 MB of native code per ABI): first paint unchanged, connected home 3 s sooner, and 5.8k methods still JIT in the first 30 s because Mono cannot precompile what only exists at runtime: generic instantiations over value types created by reflection (System.Text.Json converters, EF Core materializers, LINQ and collections over them), dynamic methods (STJ reflection emit, DI ILEmit resolvers, compiled expressions) and interop wrappers (P/Invoke, runtime-invoke, native-to-managed). Diagnose with `adb shell setprop debug.mono.log "mono_log_level=debug,mono_log_mask=aot"` (one `AOT: FOUND method` / `AOT NOT FOUND:` line per method, tag `Mono`). A custom AOT profile records those runtime instantiations, which is what the static compiler misses. Recording build: `dotnet build src/Clients/MAUI/K7.Clients.MAUI.csproj -f net10.0-android -c Release -p:K7AotProfiling=true -t:Install` (the switch turns AOT off so every method is JITted and visible, and references `Mono.AotProfiler.Android`, the only source of `libmono-profiler-aot.so`: the SDK does not ship it, and without it the runtime logs `The 'aot' profiler wasn't found in the main executable` and `aprofutil` fails with `Input file is too small`). Then on a woken device: `adb shell setprop debug.mono.profile aot:port=9999`, `adb forward tcp:9999 tcp:9999`, `adb shell am start -S -n com.k7.maui/<MainActivity>`, exercise the app (home, a detail page, a playback), and while it is still in the foreground run `aprofutil -s -v -p 9999 -o src/Clients/MAUI/custom.aprof` from the package `tools` folder in the NuGet cache, then clear the property with `setprop debug.mono.profile ''`. `-t:BuildAndStartAotProfiling` / `-t:FinishAotProfiling` are the SDK targets for the same sequence. The resulting `custom.aprof` next to the csproj is picked up automatically by the Release build when `AndroidEnableProfiledAot` is true, alongside the default profiles; re-record it when startup code changes a lot.
- Cast SDK JNI initializes on first cast use, not in `AndroidCastService` ctor.
- **Native first-gate shell** (native select-profile) is gated: only if phone interactive stays above ~1.5 s or TV above ~3 s after the hybrid wins above. Do not start that port without a new baseline.

**Smoke after startup changes:**

- Phone + TV: select-profile, solo home, first video play (lazy Exo), TV D-pad on picker.
- Android Auto headless: cold start from car only, browse + play, auth still valid, now-playing / scrobble path.
- Music session: play on phone, kill process, relaunch (restore). Play on Auto, disconnect, reopen phone. Pause still flushes.

Player scripts (`video.min.js`, videoplayer, audioplayer, ambient theme) load after first paint through `K7.ensurePlaybackAssets` (`playbackAssets.js`) on MAUI / Windows / Web, and are awaited if play happens before the prefetch finishes. The web host loads the Google Cast sender with `async`: only the device picker uses it.

Theme songs stream through the HTML audio element wherever it can be authenticated without a token in the URL: the web client gives it the same-origin theme URL (cookie), Android loads `https://k7-theme-proxy/{mediaId}` with `fetch()` into a blob (the WebView media stack rejects intercepted responses, `fetch` does not), and `BlazorPage.OnWebResourceRequested` replays that request against the server through the app `HttpClient` (bearer header). Both go through `IThemeSongSourceResolver` and `IAmbientThemeService.TryKeepOrStartStreamingAsync`. Other heads, and any stream that fails to start, fall back to the byte download handed to `K7.AmbientTheme.playBytes`.

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
