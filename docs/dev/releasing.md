# Releasing

## Version flow

1. PRs merge to `main` with Conventional Commit titles and changelog labels.
2. **release-drafter** keeps a draft GitHub Release updated (`.github/workflows/release-drafter.yml`, `.github/release-drafter.yml`).
3. Maintainers publish the release (tag `vX.Y.Z`).
4. **sync-version** rewrites `<Version>` in `Directory.Build.props` to match the tag and commits `chore: sync version to ...`.
5. **docker-release** builds and pushes `ghcr.io/kaybi-gh/k7` with semver tags and `latest`, passing `APP_VERSION` as a Docker build-arg.

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
