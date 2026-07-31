# K7 Import Tool

CLI tool to import media data (watch history, ratings, playlists) from external services into K7.

## Supported Sources

| Source | Watch History | Ratings | Playlists | Notes |
|---|---|---|---|---|
| **Plex** | No | Yes (0-10 scale) | Yes (static only by default) | Per-user via accountID when available. Smart/dynamic playlists are skipped unless `--include-dynamic-playlists` |
| **Jellyfin** | No | Yes (like=10, dislike=1) | Yes (incl. Liked Songs from favorites) | No per-play timestamps, use Tracearr for history. Heart favorites become a "Liked Songs" playlist (Audio) plus "Favoris" for movies/episodes |
| **Tracearr** | Yes | No | No | Per-play history with timestamps and provider IDs |
| **Tautulli** | Yes (per-play sessions + aggregated) | No | No | History with timestamps, transcode and device metadata |
| **Spotify** | Full (via data export) or partial (last 50 via API) | Liked songs = 10 (API) | Yes (API or `Playlist*.json` export) | Use `--spotify-data-dir` for history and/or account-data playlists |

### What gets imported

| Data type | Description |
|---|---|
| **history** | Play count, last played position, completion status, last played date. Per-play sessions (Tracearr, Tautulli, Spotify export) include device/platform when available. Re-importing skips duplicate playback sessions. |
| **ratings** | User ratings (mapped to a 0-10 scale) |
| **playlists** | Playlist titles (prefixed with source, e.g. `Jellyfin - Liked Songs`) and their items (matched by provider IDs, file path, then title/artist identity). Re-import merges into existing playlists by title. Plex smart/dynamic playlists are skipped by default (see below). |

You can select which data types to import with the `--include` option (see below).

## Prerequisites

- A K7 server instance running and accessible
- An **administrator** account on K7
- An API key or token from the source service
- A **database backup** taken just before the import (see below)

### Backup before import (strongly recommended)

There is **no undo / rollback** for a finished or partial import. The tool writes watch states, ratings, playback sessions, playlists, temp users, and optionally virtual media directly into K7. Stopping the process mid-run only stops further writes; data already committed stays.

Before a real import:

1. Back up the database (and ideally `Paths:Config`) - see [Backup and troubleshooting](../../docs/admin/backup-and-troubleshooting.md)
2. Optionally run with `--dry-run` to preview matching without writing
3. Prefer `--only-match-existing` on a first pass if you want to avoid creating virtual media

If an import goes wrong, restore that pre-import backup. Partial cleanup in the UI (delete temp `plex-*` users, playlists, virtual media) is possible but incomplete and error-prone compared to a restore.

### Getting Source API Keys

- **Plex**: get an `X-Plex-Token` from the Plex Web App (not from Authorized Devices):
  1. Sign in at https://app.plex.tv and open your server
  2. Open any single media item (movie, episode, or track - not a collection)
  3. Open the overflow menu (three dots) > **Get Info** > **View XML**
  4. In the browser address bar, copy only the value after `X-Plex-Token=` (do not include the parameter name)
  5. Pass that value to `--source-api-key`, and point `--source-url` at your PMS (e.g. `http://192.168.1.10:32400`)

  Quick check: `http://YOUR_PMS:32400/?X-Plex-Token=YOURTOKEN` should return XML/JSON for the server. A 401 means the token is wrong or revoked.

  See also: [Plex Support - Finding an authentication token](https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/)
- **Jellyfin**: Dashboard > API Keys > Create
- **Tautulli**: Settings > Web Interface > API Key
- **Tracearr**: Settings > Generate API Key
- **Spotify**: Generate an access token at https://developer.spotify.com/console with `user-library-read`, `user-read-recently-played`, and `playlist-read-private` scopes

### Spotify Data Export

Spotify offers two related downloads from https://www.spotify.com/account/privacy/ > "Request your data". Put the extracted JSON folder on `--spotify-data-dir`.

| Export | Files | What K7 Import uses |
|---|---|---|
| **Extended streaming history** | `endsong_*.json`, `StreamingHistory_*.json` | Full listen history (play counts + per-play sessions). Plays shorter than 30 seconds are skipped. |
| **Account data** | `Playlist*.json` (`{ "playlists": [ ... ] }`) | Playlists (when no Spotify API token is provided). |

Without an API token, playlists are read from `Playlist*.json`. With `--source-api-key`, playlists come from the Spotify Web API instead. Liked songs / ratings still require the API (`saved-tracks`).

`Playlist*.json` alone does **not** contain listen history - request **Extended streaming history** for that.

## Installation

```bash
dotnet build tools/K7.Import/K7.Import.csproj -c Release
```

The executable is at `tools/K7.Import/bin/Release/net10.0/k7-import`.

## Usage

```
k7-import --source <source> --source-api-key <key> --k7-url <url> [options]
```

### Required Options

| Option | Description |
|---|---|
| `--source`, `-s` | Source type: `plex`, `jellyfin`, `tautulli`, `tracearr`, or `spotify` |
| `--source-api-key` | API key or access token for the source (not needed for spotify with `--spotify-data-dir`) |
| `--k7-url` | URL of your K7 server (e.g. `http://localhost:7080`) |

### Optional

| Option | Description |
|---|---|
| `--source-url` | Source server URL (required for plex, jellyfin, tautulli; not needed for spotify) |
| `--dry-run` | Preview what would be imported without making any changes |
| `--include` | Data types to import: `history`, `ratings`, `playlists` (default: all, repeatable) |
| `--spotify-data-dir` | Path to Spotify export folder (`endsong_*` / `StreamingHistory_*` for history, `Playlist*.json` for playlists) |
| `--user-mapping` | Map a source user to an existing K7 user (format: `sourceUser:k7User`, repeatable) |
| `--auto-map-users` | Auto-map source users to K7 users with the same username (case-insensitive). Off by default |
| `--include-dynamic-playlists` | Import Plex smart/dynamic playlists as **static** snapshots. Off by default |
| `--only-match-existing` | Only import data for media that already exists in K7 - skip virtual media creation for unmatched items |
| `--fetch-metadata` | Fetch rich metadata (posters, descriptions, etc.) for newly created media |
| `--playcount-mode` | Play count merge strategy: `additive` (sum) or `max` (highest wins). Default: `additive` |
| `--rating-mode` | Rating conflict strategy: `keep` (keep existing) or `overwrite`. Default: `keep` |
| `--progress-mode` | Progress conflict strategy: `recent` (most recent wins) or `overwrite`. Default: `recent` |
| `--path-map` | Map Plex path prefix to K7 indexed path prefix (`plex:k7` or `plex=>k7`). Repeatable. Auto-deduced when omitted |

### Examples

**Import from Plex:**
```bash
k7-import -s plex \
  --source-url http://192.168.1.10:32400 \
  --source-api-key "your-plex-token" \
  --k7-url http://localhost:7080
```

**Import from Jellyfin with user mapping:**
```bash
k7-import -s jellyfin \
  --source-url http://192.168.1.10:8096 \
  --source-api-key "your-jellyfin-api-key" \
  --k7-url http://localhost:7080 \
  --user-mapping "john:john" --user-mapping "jane:jane"
```

**Dry run from Tautulli:**
```bash
k7-import -s tautulli \
  --source-url http://192.168.1.10:8181 \
  --source-api-key "your-tautulli-api-key" \
  --k7-url http://localhost:7080 \
  --dry-run
```

**Import Spotify playlists and liked songs:**
```bash
k7-import -s spotify \
  --source-api-key "your-spotify-access-token" \
  --k7-url http://localhost:7080
```

**Import full Spotify listening history from data export (no API token needed):**
```bash
k7-import -s spotify \
  --k7-url http://localhost:7080 \
  --spotify-data-dir ~/Downloads/my_spotify_data/Spotify\ Extended\ Streaming\ History \
  --include history
```

**Import only playlists from Jellyfin:**
```bash
k7-import -s jellyfin \
  --source-url http://192.168.1.10:8096 \
  --source-api-key "your-jellyfin-api-key" \
  --k7-url http://localhost:7080 \
  --include playlists
```

**Import history from Tracearr:**
```bash
k7-import -s tracearr \
  --source-url http://192.168.1.10:7878 \
  --source-api-key "your-tracearr-api-key" \
  --k7-url http://localhost:7080 \
  --include history
```

## Authentication with K7

The tool uses **OpenID Connect device code flow**. When you run the command:

1. The tool displays a URL and a one-time code
2. Open the URL in your browser and enter the code
3. Log in with your K7 **administrator** account
4. The tool automatically continues once authorized

No API key or password is passed on the command line for K7.

## User Mapping

When no `--user-mapping` is provided, the tool creates **temporary users** on K7 (e.g. `plex-john`, `jellyfin-jane`). You can then merge these into real K7 users via the admin UI (Settings > Users > merge button).

With `--auto-map-users`, source users whose name matches an existing K7 username (case-insensitive) are mapped automatically; remaining users still get temp accounts.

With `--user-mapping`, data is imported directly into existing K7 users:

```bash
--user-mapping "PlexUser:k7user" --user-mapping "AnotherUser:anotherk7user"
```

## Plex Dynamic Playlists

Plex smart (dynamic) playlists are **skipped by default**. Their filter rules do not map cleanly to K7, and importing the current item list would freeze a stale snapshot.

**Recommended:** recreate them as K7 dynamic playlists (rules that refresh with the library) instead of importing a frozen list.

If you still want a one-shot static copy of the current contents:

```bash
k7-import -s plex \
  --source-url http://192.168.1.10:32400 \
  --source-api-key "your-plex-token" \
  --k7-url http://localhost:7080 \
  --include-dynamic-playlists
```

## Media Matching

Items are matched between the source and K7 in this order:

1. **External IDs** (TMDb, IMDb, TVDb, MusicBrainz recording / release-group, ISRC, then other providers)
2. **File path** (Plex `Media.Part.file`, remapped with `--path-map` and/or auto-deduced mount prefixes)
3. **Title / identity** via bulk create (links to an existing indexed media when identity matches, otherwise creates virtual media unless `--only-match-existing`)

MusicBrainz notes: K7 albums use `musicbrainz` = release-group. Plex album/release MBIDs are imported as `musicbrainz-release` so they do not collide. Track MBIDs (recordings) keep the `musicbrainz` key and match K7 tracks after metadata refresh.

### Path mapping

When Plex and K7 see different mount points for the same files:

```bash
# Your typical Docker layout (Plex /data/media/... vs K7 /media/...)
--path-map "/data/media/Videos=>/media" \
--path-map "/data/media/Musiques=>/media/Musiques"

# Or one map per library root
--path-map "/data/media/Videos/Animes=>/media/Animes" \
--path-map "/data/media/Videos/Series=>/media/Series" \
--path-map "/data/media/Videos/Films=>/media/Films" \
--path-map "/data/media/Musiques=>/media/Musiques"

# Windows drive letters (prefer => to avoid ambiguity)
--path-map "D:/PlexMedia=>E:/K7Media"
```

If `--path-map` is omitted, the tool samples Plex paths and matching K7 indexed filenames to deduce common prefix remaps automatically.

### Import summary

The summary separates:

| Metric | Meaning |
|---|---|
| Matched via external ID / path / title | Existing K7 media found |
| Created virtual media | New lightweight media created for unmatched source items |
| Unmatched items | No match and virtual creation disabled or not applicable |

Unmatched titles are listed at the end of the import.

## Merge Strategy

When importing watch states for items that already have data in K7, the merge strategy determines how conflicts are resolved. You can configure each dimension independently:

| Dimension | Modes | Default |
|---|---|---|
| **Play count** | `additive` (sum source + target) or `max` (keep highest) | `additive` |
| **Rating** | `keep` (don't overwrite existing) or `overwrite` | `keep` |
| **Progress** | `recent` (most recent interaction wins) or `overwrite` (source always wins) | `recent` |

Example: import with additive play counts and overwrite ratings:
```bash
k7-import -s spotify --k7-url http://localhost:7080 \
  --spotify-data-dir ~/spotify-data \
  --playcount-mode additive --rating-mode overwrite
```

## Virtual Media Creation

By default, unmatched source items are created as **virtual media** in K7 - lightweight entities without physical media files. This ensures all watch history, ratings, and play counts are preserved, even if K7 doesn't have the corresponding media files yet.

Virtual media can later be enriched with metadata or linked to real media files when they become available.

To automatically fetch rich metadata (posters, descriptions, ratings) for newly created media, use `--fetch-metadata`. This queues background metadata tasks for enrichable types (movies, albums, series) that have external IDs:
```bash
k7-import -s tautulli --source-url http://localhost:8181 --source-api-key YOUR_KEY --k7-url http://localhost:7080 --fetch-metadata
```

To disable virtual media creation and only import data for items already in K7:
```bash
k7-import -s spotify --k7-url http://localhost:7080 --only-match-existing
```
