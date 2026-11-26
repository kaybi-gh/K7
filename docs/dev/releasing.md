# Releasing

## Version flow

1. PRs merge to `main` with Conventional Commit titles and changelog labels.
2. **release-drafter** keeps a draft GitHub Release updated (`.github/workflows/release-drafter.yml`, `.github/release-drafter.yml`).
3. Maintainers prepare a **draft** GitHub Release with tag name `vX.Y.Z` (target `main`). Saving a draft does **not** create the git tag yet; that is normal.
4. **client-release** uploads native clients to the draft. If the git tag is missing, the workflow creates it from the release target commitish (usually `main`), then builds:
   - `K7-{version}-android.apk` - sideload / Android TV. `versionCode` is `(major * 10000 + minor * 100 + patch) * 100`, so `1.18.0` is `1180000`. PR builds of that version use `1180001` through `1180099`. The `1.18.1` release is `1180100`.
   - `K7-{version}-win-x64.zip` - self-contained unpackaged Windows. Extract the whole folder and run `K7.exe` (needs matching `K7.pri` sidecars, not a single-file exe). Requires WebView2 Runtime and a recent Windows App Runtime on the machine.
   - `K7-{version}-ios-sideload.ipa` - ad-hoc-signed iOS client for AltStore / SideStore / Sideloadly, plus AltStore source `apps.json`
   - `K7-{version}-linux-x64.flatpak` - Flatpak bundle (`flatpak install --user ./K7-...-linux-x64.flatpak`), the only Linux package. GNOME Platform 49 provides GTK4 / WebKitGTK / GStreamer, libvlc 4 is inside the app (see [Linux libvlc bundle](#linux-libvlc-bundle)). Installs the desktop entry and the `k7://` scheme handler. See [Linux Flatpak](#linux-flatpak).
5. Maintainers **publish the draft** (human / non-`GITHUB_TOKEN`). That triggers **sync-version** and **docker-release**.
6. **sync-version** rewrites `<Version>` in `Directory.Build.props` to match the tag and commits `chore: sync version to ...`.
7. **docker-release** builds and pushes `ghcr.io/kaybi-gh/k7` and `ghcr.io/kaybi-gh/k7-import` with semver tags and `latest`, passing `APP_VERSION` as a Docker build-arg.

Example:

```bash
# Draft from the UI (choose tag vX.Y.Z, target main, Save draft) or:
gh release create vX.Y.Z --draft --target main --title "K7 vX.Y.Z" --notes-file notes.md
# Editing a draft does not re-run clients. Drafter-created drafts also do not
# auto-start other workflows (GITHUB_TOKEN). Start clients once:
gh workflow run client-release.yml -f tag=vX.Y.Z
# After APK, Windows zip, Linux Flatpak, and iOS IPA appear on the draft:
gh release edit vX.Y.Z --draft=false
```

Manual re-attach: Actions -> Client release -> Run workflow with the tag (creates the git tag if still missing).

Pull requests do not upload these assets. For a one-off build of a PR (Android, Windows, Linux, iOS), run **PR client builds** and let it comment the links. See [developing.md](developing.md#pr-client-builds).

iOS ships as a sideload IPA on that draft. No Apple Developer certificate is required. The sideloader re-signs with the user's Apple ID at install. Users add a stable AltStore/SideStore source:

`https://github.com/kaybi-gh/K7/releases/latest/download/apps.json`

`apps.json` uses a fixed name so `latest/download/` tracks the newest published release. The IPA URL inside `apps.json` points at that release's versioned asset. The source icon is [`branding/icon.png`](../../branding/icon.png). Details: [`altstore/README.md`](../../altstore/README.md). Mac Catalyst packages are not published.

## Release notes

Draft body comes from `.github/release-drafter.yml`. Section order:

1. **Breaking changes** - curated summary (edit before publish; replace the placeholder with "None" or short bullets)
2. **Highlights** - 1-3 manual bullets (edit before publish)
3. **Changes** - full categorized list via `$CHANGES` (Breaking Changes, Features, Bug Fixes, Documentation, Miscellaneous)
4. **Artifacts** - Docker image and client asset names with `$RESOLVED_VERSION`
5. **New contributors** - via `$NEW_CONTRIBUTORS` (first-time PR authors only)

Edit **Highlights** and the top **Breaking changes** summary just before you publish. Release Drafter regenerates the draft on pushes to `main`, so earlier manual edits may be overwritten.

Client assets land on the draft; publishing the draft triggers **docker-release**. Artifact references in the notes:

| Artifact | Path / reference |
|---|---|
| Docker (server) | `ghcr.io/kaybi-gh/k7:$RESOLVED_VERSION` (also `latest`) |
| Docker (import) | `ghcr.io/kaybi-gh/k7-import:$RESOLVED_VERSION` (also `latest`) |
| Android | Release asset `K7-{version}-android.apk` |
| Windows | Release asset `K7-{version}-win-x64.zip` |
| iOS | Release assets `K7-{version}-ios-sideload.ipa`, `apps.json` |
| Linux | Release asset `K7-{version}-linux-x64.flatpak` |

### Linux Flatpak

Release CI packs the same self-contained publish tree (with the libvlc bundle) into a single-file bundle with [`tools/linux/build-flatpak.sh`](../../tools/linux/build-flatpak.sh); manifest, wrapper and desktop files live under [`tools/linux/flatpak/`](../../tools/linux/flatpak/). App id `com.k7.maui` (Flatpak requires reverse-DNS). The host only needs Flatpak and the GNOME Platform 49 runtime, pulled from Flathub on first install together with its `codecs-extra` extension (H.264 / AAC for WebKitGTK HLS; `ffmpeg-full` and `openh264` no longer exist on freedesktop 25.08).

It is the only Linux package. The `.deb` and the `.tar.gz` that shipped the same publish tree were dropped: they bundled libvlc but depended on the host GTK4, WebKitGTK and GStreamer versions (Ubuntu 24.04 in practice), and the HLS player behaved differently per host WebKitGTK, which made them impossible to test from one machine. The publish tree can still be run directly for development on a GTK4 / WebKitGTK 6.0 host (`./K7.Clients.MAUI` from the publish folder; `LinuxPackaging` then registers the per-user `k7://` handler), but that is a developer workflow, not a shipped package. A bundle does not self-update: users install the new release's `.flatpak` the same way (a self-hosted Flatpak repository would enable `flatpak update`).

```bash
flatpak install --user ./K7-*-linux-x64.flatpak
flatpak run com.k7.maui 2>&1 | tee /tmp/k7.log
```

What the sandbox changes compared with running the publish tree directly on a GTK4 host, all handled by the wrapper (`/app/bin/k7`) or the manifest:

- WebKit's nested bubblewrap sandbox cannot start inside Flatpak: the wrapper exports `K7_WEBKIT_DISABLE_SANDBOX=1`.
- libvlc's HTTP access asks the `proxy` helper (libproxy) for every URL; the runtime one may hand back a proxy for the loopback `VlcAuthProxy`, so `/app/bin/proxy` answers `direct://` and is first on `PATH`.
- The GNOME runtime has no `libbz2.so.1.0` soname and no `libbsd` / `libmd`: `bundle-libvlc.sh` ships them. It ships no `libva*` (the runtime GStreamer needs its own, newer libva).
- The wrapper selects the GSK GL renderer (`GSK_RENDERER=ngl`): the runtime GTK defaults to Vulkan, which aborts on live window resize on software Vulkan (Hyper-V, WSLg, xrdp guests). `GSK_RENDERER=cairo` is the slow but safest override if GL misbehaves. Under WSL (kernel banner, `/mnt/wslg` is not mounted in the sandbox) it also switches WebKit to its software path (`WEBKIT_DISABLE_DMABUF_RENDERER=1`); on other machines, virtual or not, the DMA-BUF renderer must stay on: disabling it on a Hyper-V guest left Video.js HLS on an endless spinner.
- libvlc's own HTTP module cannot connect to the loopback proxy inside the sandbox (root cause unidentified: resolution succeeds, the connect fails before any packet reaches the listener, with or without TCP Fast Open). Linux Direct Play therefore feeds libvlc through media callbacks (`LinuxHttpRangeMediaInput`, .NET HTTP Range requests; the bundle ships `access/libimem_plugin.so`). `K7_VLC_ACCESS=http` selects the libvlc HTTP module again for comparison.
- Per-user `k7://` registration is skipped (`LinuxPackaging.IsFlatpak`): the exported desktop entry already carries `MimeType=x-scheme-handler/k7`.

Not done on purpose: no `WEBKIT_DISABLE_COMPOSITING_MODE` (it breaks MSE / HLS), no software GL or renderer overrides, no Flathub submission (prebuilt bundle only). Local build on Ubuntu 24.04 after `dotnet publish ... -o /tmp/k7-linux` with the libvlc bundle in place:

```bash
sudo apt install flatpak flatpak-builder
bash tools/linux/build-flatpak.sh /tmp/k7-linux 1.16.0 /tmp/K7-1.16.0-linux-x64.flatpak
```

On WSL keep the publish dir and the output under `/tmp` (flatpak-builder wants one filesystem).
### Linux libvlc bundle

Windows ships libvlc 4 through the `VideoLAN.LibVLC.Windows` NuGet (copied to `libvlc/win-x64`). No such package exists for Linux, and distros only ship VLC 3, so the Linux job builds the bundle itself with [`tools/linux/bundle-libvlc.sh`](../../tools/linux/bundle-libvlc.sh):

1. Downloads the VideoLAN `vlc` snap from its edge channel (nightly VLC 4, base core24), pinned by revision and SHA-256 (`VLC_SNAP_REVISION` / `VLC_SNAP_SHA256` at the top of the script).
2. Extracts `libvlc.so.12`, `libvlccore.so.9`, `libvlc_pulse` and the plugin subset K7 uses (file and HTTP input, demuxers, avcodec and the other decoders, packetizers, audio filters and mixers, Pulse output, software chroma converters, deinterlace and scale filters, the vmem video output). Encoders, hardware decoders, subtitle renderers and network protocols K7 never plays through libvlc are dropped.
3. The snap links those plugins against Ubuntu 24.04 libraries it does not contain (ffmpeg 6.1, dav1d, libmatroska, libva). The script resolves them with `ldd` on the runner (`vlc-plugin-base` is installed as a dependency magnet) and copies them to `lib/`. Libraries every GTK4 / WebKitGTK desktop and the GNOME runtime already have stay on the host (`HOST_LIBS` in the script). `libxml2.so.2` is private as well: newer distributions only ship that soname inside `libxml2-16`.
4. Writes `BUNDLE.txt` (VLC version, plugins, private and host libraries) and fails if anything is left unresolved.

The output lands in `src/Clients/MAUI/libvlc/linux-x64` (gitignored). `K7CopyLibVlcLinuxNatives` / `K7PublishLibVlcLinuxNatives` copy it next to the app. At run time `LinuxLibVlcBundle` finds it (or `K7_LIBVLC_DIR`) and `LinuxVlcVideoPlayer` loads the private libraries and libvlccore by full path before libvlc, then sets `VLC_PLUGIN_PATH`. Without the directory the client still runs and uses a system VLC 4 when there is one.

### Android signing

Prefer repository secrets so APK updates keep a stable signature:

| Secret | Purpose |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Base64-encoded `.keystore` / `.jks` |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Key alias |
| `ANDROID_KEY_PASSWORD` | Key password (defaults to store password if unset) |
| `RENOVATE_TOKEN` | Dependency update bot (see [Developing - Dependency updates](developing.md#dependency-updates)) |

If secrets are missing, CI generates an ephemeral keystore (sideload only; signature changes each run).

## Labels

### Changelog (required on PRs)

`pr-label-check.yml` requires at least one of: `breaking-change`, `enhancement`, `bug`, `chore`, `documentation`, `skip-changelog`.

### Path labels (automatic)

`.github/labeler.yml` via `label-pr.yml`: `server`, `clients`, `ci`, `tests`, etc. release-drafter also autolabels from Conventional Commit prefixes.

## Docker images

| Item | Value |
|---|---|
| Server image | `ghcr.io/kaybi-gh/k7` (`Dockerfile`) |
| Import image | `ghcr.io/kaybi-gh/k7-import` (`tools/K7.Import/Dockerfile`) |
| Trigger | Published GitHub Release |
| Build arg | `APP_VERSION` -> `dotnet publish -p:Version=...` |

Post-push Trivy scan (`docker-release`) is best-effort: the step uses `continue-on-error` so a flaky Trivy binary install does not fail the release after the image is already on GHCR. Treat scan failures as advisory and re-run or scan locally if needed.

Operator upgrade notes: [Install - Upgrades](../admin/install.md#upgrades).

Contributors do not cut releases from a PR - focus on correct labels and commit titles so draft notes stay accurate.

## Demo media and screenshots

### Demo media

[`tools/K7.Demo/download-demo-media.sh`](../../tools/K7.Demo/download-demo-media.sh) downloads sample movies, series, and music into a library root (default `MEDIA_ROOT=/k7/media`). Useful for local demos and screenshot capture. Requires a Unix-like shell (`bash`, `curl`, etc.).

```bash
MEDIA_ROOT=/path/to/media ./tools/K7.Demo/download-demo-media.sh
```

Point a K7 library at that folder and scan.

### Screenshots

README gallery images live in [`screenshots/`](../../screenshots/). Capture tooling:

- Guide: [`tools/K7.Demo/generate-screenshots/README.md`](../../tools/K7.Demo/generate-screenshots/README.md)
- Default target: live demo `https://k7.kaybi.dev`
- Commands: `npm run capture`, `npm run composite:movie`

Requires Node.js, Playwright browsers, and a reachable demo (or reconfigured URL in `screenshots.config.json`).
