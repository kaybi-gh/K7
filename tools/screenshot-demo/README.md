# Screenshot capture

Playwright scripts to capture UI screenshots from the live demo for the README gallery.

Output: [`screenshots/`](../../screenshots/) at the repository root.

## Setup

```bash
cd tools/screenshot-demo
npm install
npx playwright install chromium
```

## Capture all screenshots

```bash
npm run capture
```

Configuration: [`screenshots.config.json`](screenshots.config.json) (demo URL, device profiles, page paths, media IDs).

### Capture a subset

```powershell
# One or more profiles
$env:K7_SCREENSHOTS_PROFILES = "desktop,mobile"
$env:K7_SCREENSHOTS_FILES = "movie-detail-sintel-desktop.png,movie-detail-sintel-mobile.png"
npm run capture
```

## Device showcase composite

Builds `screenshots/movie-showcase-devices.png` from existing captures (TV home, Sintel on laptop and phone):

```bash
npm run composite:movie
```

Background modes (`K7_SHOWCASE_BACKGROUND`):

| Value | Effect |
| --- | --- |
| `transparent` | Fully transparent PNG (default) |
| `fade` | Soft radial fade to transparent |
| `solid` | Solid `#10141b` background |

Optional: `K7_SHOWCASE_PADDING=32` (crop margin around devices, default 32).

## Profiles

| Profile | Viewport / device | Notes |
| --- | --- | --- |
| `desktop` | 1440x900 | Default browser layout |
| `tv` | 1920x1080 | Couch mode (`platform-tv` + `getParsedUserAgent` override) |
| `mobile` | 412x915 @2x (824x1830 PNG) | Pixel 8/9 viewport, bottom navigation layout |

Guest login is enabled by default against `https://k7.kaybi.dev`.
