# K7 Import Tool

CLI tool to import media data (watch history, ratings, playlists) from external services into K7.

## Supported Sources

| Source | Watch History | Ratings | Playlists | Notes |
|---|---|---|---|---|
| **Plex** | No | Yes (0-10 scale) | Yes | No per-play timestamps, use Tracearr for history |
| **Jellyfin** | No | Yes (like=10, dislike=1) | Yes | No per-play timestamps, use Tracearr for history |
| **Tracearr** | Yes | No | No | Per-play history with timestamps |
| **Tautulli** | Yes (aggregated) | No | No | History only, aggregated from play logs |
| **Spotify** | Full (via data export) or partial (last 50 via API) | Liked songs = 10 | Yes | Use `--spotify-data-dir` for full history |

### What gets imported

| Data type | Description |
|---|---|
| **history** | Play count, last played position, completion status, last played date |
| **ratings** | User ratings (mapped to a 0-10 scale) |
| **playlists** | Playlist titles and their items (matched by provider IDs) |

You can select which data types to import with the `--include` option (see below).

## Prerequisites

- A K7 server instance running and accessible
- An **administrator** account on K7
- An API key or token from the source service

### Getting Source API Keys

- **Plex**: Settings > Account > Authorized Devices, or use your X-Plex-Token (found in the XML of any library request)
- **Jellyfin**: Dashboard > API Keys > Create
- **Tautulli**: Settings > Web Interface > API Key
- **Tracearr**: Settings > Generate API Key
- **Spotify**: Generate an access token at https://developer.spotify.com/console with `user-library-read`, `user-read-recently-played`, and `playlist-read-private` scopes

### Spotify Full Listening History

The Spotify API only exposes the last 50 recently played tracks. For a **complete** listening history, request your data export from Spotify:

1. Go to https://www.spotify.com/account/privacy/ > "Request your data"
2. Select **Extended streaming history**
3. Wait for the email (can take up to 30 days)
4. Download and extract the ZIP
5. Pass the folder path with `--spotify-data-dir`

The tool reads both `endsong_*.json` (extended format) and `StreamingHistory_music_*.json` (basic format). Plays shorter than 30 seconds are skipped.

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
| `--k7-url` | URL of your K7 server (e.g. `http://localhost:5000`) |

### Optional

| Option | Description |
|---|---|
| `--source-url` | Source server URL (required for plex, jellyfin, tautulli; not needed for spotify) |
| `--dry-run` | Preview what would be imported without making any changes |
| `--include` | Data types to import: `history`, `ratings`, `playlists` (default: all, repeatable) |
| `--spotify-data-dir` | Path to Spotify extended streaming history export folder (for full listen history) |
| `--user-mapping` | Map a source user to an existing K7 user (format: `sourceUser:k7User`, repeatable) |
| `--create-missing` | Create virtual media entities for unmatched items (default: `true`) |
| `--no-create-missing` | Disable virtual media creation — only import data for already-matched items |
| `--playcount-mode` | Play count merge strategy: `additive` (sum) or `max` (highest wins). Default: `additive` |
| `--rating-mode` | Rating conflict strategy: `keep` (keep existing) or `overwrite`. Default: `keep` |
| `--progress-mode` | Progress conflict strategy: `recent` (most recent wins) or `overwrite`. Default: `recent` |

### Examples

**Import from Plex:**
```bash
k7-import -s plex \
  --source-url http://192.168.1.10:32400 \
  --source-api-key "your-plex-token" \
  --k7-url http://localhost:5000
```

**Import from Jellyfin with user mapping:**
```bash
k7-import -s jellyfin \
  --source-url http://192.168.1.10:8096 \
  --source-api-key "your-jellyfin-api-key" \
  --k7-url http://localhost:5000 \
  --user-mapping "john:john" --user-mapping "jane:jane"
```

**Dry run from Tautulli:**
```bash
k7-import -s tautulli \
  --source-url http://192.168.1.10:8181 \
  --source-api-key "your-tautulli-api-key" \
  --k7-url http://localhost:5000 \
  --dry-run
```

**Import Spotify playlists and liked songs:**
```bash
k7-import -s spotify \
  --source-api-key "your-spotify-access-token" \
  --k7-url http://localhost:5000
```

**Import full Spotify listening history from data export (no API token needed):**
```bash
k7-import -s spotify \
  --k7-url http://localhost:5000 \
  --spotify-data-dir ~/Downloads/my_spotify_data/Spotify\ Extended\ Streaming\ History \
  --include history
```

**Import only playlists from Jellyfin:**
```bash
k7-import -s jellyfin \
  --source-url http://192.168.1.10:8096 \
  --source-api-key "your-jellyfin-api-key" \
  --k7-url http://localhost:5000 \
  --include playlists
```

**Import history from Tracearr:**
```bash
k7-import -s tracearr \
  --source-url http://192.168.1.10:7878 \
  --source-api-key "your-tracearr-api-key" \
  --k7-url http://localhost:5000 \
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

With `--user-mapping`, data is imported directly into existing K7 users:

```bash
--user-mapping "PlexUser:k7user" --user-mapping "AnotherUser:anotherk7user"
```

## Media Matching

Items are matched between the source and K7 using external provider IDs (TMDb, IMDb, TVDb, MusicBrainz, ISRC). The matching priority is:

1. TMDb
2. IMDb
3. TVDb
4. MusicBrainz / ISRC
5. Any other provider

Unmatched items are listed in the summary at the end of the import.

## Merge Strategy

When importing watch states for items that already have data in K7, the merge strategy determines how conflicts are resolved. You can configure each dimension independently:

| Dimension | Modes | Default |
|---|---|---|
| **Play count** | `additive` (sum source + target) or `max` (keep highest) | `additive` |
| **Rating** | `keep` (don't overwrite existing) or `overwrite` | `keep` |
| **Progress** | `recent` (most recent interaction wins) or `overwrite` (source always wins) | `recent` |

Example: import with additive play counts and overwrite ratings:
```bash
k7-import -s spotify --k7-url http://localhost:5000 \
  --spotify-data-dir ~/spotify-data \
  --playcount-mode additive --rating-mode overwrite
```

## Virtual Media Creation

By default (`--create-missing`), unmatched source items are created as **virtual media** in K7 — lightweight entities without physical media files. This ensures all watch history, ratings, and play counts are preserved, even if K7 doesn't have the corresponding media files yet.

Virtual media can later be enriched with metadata or linked to real media files when they become available.

To disable this and only import data for items already in K7:
```bash
k7-import -s spotify --k7-url http://localhost:5000 --no-create-missing
```
