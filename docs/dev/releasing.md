# Releasing

## Version flow

1. PRs merge to `main` with Conventional Commit titles and changelog labels.
2. **release-drafter** keeps a draft GitHub Release updated (`.github/workflows/release-drafter.yml`, `.github/release-drafter.yml`).
3. Maintainers create the GitHub Release as a **draft** (tag `vX.Y.Z`). The repo uses immutable releases, so assets cannot be added after publish.
4. **client-release** (`.github/workflows/client-release.yml`) runs on release creation, uploads native clients, then publishes the draft:
   - `K7-{version}-android.apk` - sideload / Android TV
   - `K7-{version}-win-x64.zip` - self-contained unpackaged Windows; run `K7.exe` inside the zip
5. Publishing the draft triggers **sync-version** and **docker-release**.
6. **sync-version** rewrites `<Version>` in `Directory.Build.props` to match the tag and commits `chore: sync version to ...`.
7. **docker-release** builds and pushes `ghcr.io/kaybi-gh/k7` with semver tags and `latest`, passing `APP_VERSION` as a Docker build-arg.

Example dry-run:

```bash
gh release create vX.Y.Z-rc.N --draft --prerelease --target main \
  --title "K7 vX.Y.Z-rc.N" \
  --notes "Dry-run of release pipelines, not the official 1.0.0"
```

Manual re-attach to an existing mutable/draft tag: Actions -> Client release -> Run workflow with the tag.

iOS / Mac Catalyst packages are not published from CI (Apple signing required).

## Release notes

Draft body comes from `.github/release-drafter.yml`. Section order:

1. **Breaking changes** - curated summary (edit before publish; replace the placeholder with "None" or short bullets)
2. **Highlights** - 1-3 manual bullets (edit before publish)
3. **Changes** - full categorized list via `$CHANGES` (Breaking Changes, Features, Bug Fixes, Documentation, Miscellaneous)
4. **Artifacts** - Docker image and client asset names with `$RESOLVED_VERSION`
5. **New contributors** - via `$CONTRIBUTORS`

Edit **Highlights** and the top **Breaking changes** summary just before you publish. Release Drafter regenerates the draft on pushes to `main`, so earlier manual edits may be overwritten.

Creating the draft triggers **client-release** (upload then publish). Publishing then triggers **docker-release**. Artifact references in the notes:

| Artifact | Path / reference |
|---|---|
| Docker | `ghcr.io/kaybi-gh/k7:$RESOLVED_VERSION` (also `latest`) |
| Android | Release asset `K7-{version}-android.apk` |
| Windows | Release asset `K7-{version}-win-x64.zip` |

### Android signing

Prefer repository secrets so APK updates keep a stable signature:

| Secret | Purpose |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | Base64-encoded `.keystore` / `.jks` |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_ALIAS` | Key alias |
| `ANDROID_KEY_PASSWORD` | Key password (defaults to store password if unset) |

If secrets are missing, CI generates an ephemeral keystore (sideload only; signature changes each run).

## Labels

### Changelog (required on PRs)

`pr-label-check.yml` requires at least one of: `breaking-change`, `enhancement`, `bug`, `chore`, `documentation`, `skip-changelog`.

### Path labels (automatic)

`.github/labeler.yml` via `label-pr.yml`: `server`, `clients`, `ci`, `tests`, etc. release-drafter also autolabels from Conventional Commit prefixes.

## Docker image

| Item | Value |
|---|---|
| Image | `ghcr.io/kaybi-gh/k7` |
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
