# K7 Import Tool

CLI tool to import media data (watch history, ratings, playlists) from external services into K7.

## Supported Sources

| Source | Watch History | Ratings | Playlists | Notes |
|---|---|---|---|---|
| **Plex** | Aggregated watch states (no per-play sessions) | Yes (0-10 scale) | Yes (static only by default) | Owner + plex.tv friends via API tokens. Local Plex Home ratings need `--plex-db` (HTTP often returns the admin's stars). Smart/dynamic playlists are skipped unless `--include-dynamic-playlists`. Use Tautulli or Tracearr for per-play history. |
| **Jellyfin** | Aggregated watch states (no per-play sessions) | Yes (like=10, dislike=1) | Yes (incl. Liked Songs from favorites) | No per-play timestamps. Use Tracearr for session history. Heart favorites become a "Liked Songs" playlist (Audio) plus "Favoris" for movies/episodes |
| **Tracearr** | Yes | No | No | Requires Tracearr **2.0+** (public API v2). Per-play history with timestamps plus TMDb/IMDb/TVDb IDs when available |
| **Tautulli** | Yes (per-play sessions + aggregated) | No | No | History with timestamps, transcode and device metadata. Uses Plex `title` (not `full_title`) and parses agent guids (`tmdb` / `imdb` / `tvdb`) when present. History rows rarely include guids, so the importer also calls `get_metadata` once per series (`grandparent_rating_key`) and attaches those ids as parent-series ids. A history row tagged `episode` with no show title is treated as a movie (Plex/Tautulli sometimes mis-tags films like Parasite) |
| **Spotify** | Full (via data export) or partial (last 50 via API) | Liked songs = 10 (API) | Yes (API or `Playlist*.json` export) | Use `--spotify-data-dir` for history and/or account-data playlists |

### What gets imported

| Data type | Description |
|---|---|
| **history** | Play count, last played position, completion status, last played date. In-progress titles become item playback bookmarks. Completed series episodes update series playback bookmarks (next playable episode for Keep Watching). Per-play sessions (Tracearr, Tautulli, Spotify export) include device/platform when available. Direct Plex/Jellyfin APIs import aggregated watch states only. Re-importing skips duplicate playback sessions. |
| **ratings** | User ratings (mapped to a 0-10 scale) |
| **playlists** | Playlist titles (prefixed with source, e.g. `Jellyfin - Liked Songs`) and their items (matched by provider IDs, file path, then title/artist identity). Unmatched items become virtual file-less medias unless `--only-match-existing` (show them via "Afficher les titres indisponibles"). Re-import merges into existing playlists by title. Plex smart/dynamic playlists are skipped by default (see below). |

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
- **Tracearr** (2.0+): Settings > General > Generate API Key (Bearer `trr_pub_...`). Older Tracearr 1.x / API v1 is not supported.
- **Spotify**: there is no long-lived API key (the old Web API console / Try it token generator is deprecated). Create an app at https://developer.spotify.com/dashboard (enable **Web API**), then mint a 1-hour token:
  - **Catalog / ISRC matching** (enough for `--spotify-data-dir` history): Client Credentials. In the app Settings copy Client ID + Client Secret, then:
    ```powershell
    $r = Invoke-RestMethod -Method Post -Uri "https://accounts.spotify.com/api/token" `
      -ContentType "application/x-www-form-urlencoded" `
      -Body @{ grant_type = "client_credentials"; client_id = "YOUR_CLIENT_ID"; client_secret = "YOUR_CLIENT_SECRET" }
    $r.access_token
    ```
    Pass that `access_token` to `--source-api-key`. Do not pass the client secret.
    New Developer Dashboard apps are **Development Mode** (Feb 2026): the **app owner must be Spotify Premium**, Search/batch `GET /tracks` are limited or removed, and a 403 here usually means Premium is missing or the token cannot call catalog endpoints. Title matching still runs from the export if the API is rejected.
  - **Liked songs / live playlists / recently played**: needs a user OAuth token (`user-library-read`, `user-read-recently-played`, `playlist-read-private`). Client Credentials cannot read your library.

### Spotify Data Export

Spotify offers two related downloads from https://www.spotify.com/account/privacy/ > "Request your data". Put the extracted JSON folder on `--spotify-data-dir`.

| Export | Files | What K7 Import uses |
|---|---|---|
| **Extended streaming history** | `endsong_*.json`, `StreamingHistory_*.json` | Full listen history (play counts + per-play sessions). Plays shorter than 30 seconds are skipped. |
| **Account data** | `Playlist*.json` (`{ "playlists": [ ... ] }`) | Playlists (when no Spotify API token is provided). |

Without an API token, playlists are read from `Playlist*.json`. With `--source-api-key`, playlists come from the Spotify Web API instead. Liked songs / ratings still require the API (`saved-tracks`).

Pass both `--spotify-data-dir` and `--source-api-key` for history import: the export has Spotify track IDs but no ISRC, while K7 tracks typically have MusicBrainz ISRCs and **no** Spotify IDs. Matching then tries, in order: title/artist/album against K7, Spotify catalog `GET /v1/tracks` (ISRC, 50 ids per request; needs a working token / Premium for new Dev Mode apps), then Odesli/Songlink **only for titles still unmatched**. Odesli results are cached in `k7-spotify-id-bridge.json` under `--spotify-data-dir`. MusicBrainz live search is not used (1 request/sec, no reverse Spotify-id bulk API). ListenBrainz/MetaBrainz labs only map MBID to Spotify, not the other way. Title matching also folds curly vs ASCII apostrophes, hyphen vs space (`Cerf-volant` / `Cerf volant`), and drops `Original Version` / remaster suffixes (not Live / Remix / Acoustic).

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
| `--dry-run` | Full preview without writing: user plans, media match/create/unmatched counts, per-user history/ratings/playlists |
| `--report`, `-o` | Write the full report (including complete media lists) to a UTF-8 text file. Console then shows a summary only |
| `--tracearr-server` | Tracearr only: limit history to one backend (`plex`, `jellyfin`, `emby`, or a Tracearr server UUID). Listed at connect time |
| `--plex-db` | Plex only: path to a copy of `com.plexapp.plugins.library.db` (put `.db-wal` / `.db-shm` next to it if they exist). Required for local Home-user ratings |
| `--include` | Data types to import: `history`, `ratings`, `playlists` (default: all, repeatable) |
| `--spotify-data-dir` | Path to Spotify export folder (`endsong_*` / `StreamingHistory_*` for history, `Playlist*.json` for playlists) |
| `--user-mapping` | Map a source user to an existing K7 user (format: `sourceUser:k7User`, repeatable) |
| `--users` | Only import these source users (remote id or remote name, case-insensitive, repeatable). Default: all source users |
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

**Plex Home ratings (API token + library DB):**
```bash
k7-import -s plex \
  --source-url http://192.168.1.10:32400 \
  --source-api-key "your-plex-token" \
  --k7-url http://localhost:7080 \
  --include ratings \
  --plex-db ./com.plexapp.plugins.library.db \
  --users 20281801 \
  --user-mapping "20281801:charlotte" \
  --dry-run
```

`--source-url` and `--source-api-key` stay required: the DB supplies per-account stars, the PMS API still lists libraries and metadata for matching. Use the account id printed in `Plex DB ratings by account` (a Home display name on PMS may have a different id, or 0 rows).

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

**Import Spotify history with ISRC matching (export + API token):**
```bash
k7-import -s spotify \
  --source-api-key "your-spotify-access-token" \
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

**Dry-run with full report file (recommended for large libraries):**
```bash
k7-import -s tracearr \
  --source-url http://192.168.1.10:7878 \
  --source-api-key "your-tracearr-api-key" \
  --k7-url http://localhost:7080 \
  --include history \
  --dry-run \
  --report ./tracearr-dry-run.txt
```

**Tracearr history from Plex only (skip Jellyfin/Emby plays):**
```bash
k7-import -s tracearr \
  --source-url http://192.168.1.10:7878 \
  --source-api-key "your-tracearr-api-key" \
  --k7-url http://localhost:7080 \
  --tracearr-server plex \
  --include history \
  --dry-run \
  --report ./tracearr-plex-only.txt
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

`--users` limits **which source users** are processed (remote id or remote name, case-insensitive). It does not replace `--user-mapping`. For Plex Home ratings, prefer the SQLite account id from the `--plex-db` warning line, not only the PMS display name:

```bash
--users 20281801 --user-mapping "20281801:charlotte"
```

Plex owner id is `owner` (name is `myPlexUsername`, often the plex.tv email). Unmatched `--users` values are warned and ignored.

## Plex per-user ratings and playlists

A Plex admin token is **not** enough for every user. PMS `userRating` and `/playlists` follow the request token, not `accountID`. There is no official "ratings for this Home profile" HTTP call.

| Who | What works | How |
|---|---|---|
| Server owner (admin token) | Ratings + static playlists | `--source-api-key` only |
| plex.tv friends (shared server) | Ratings + playlists when plex.tv returns their `accessToken` | Same token; no `--plex-db` |
| Local Plex Home profiles | Star ratings in SQLite only | `--plex-db` (see below) |
| Home profile with a PIN | HTTP switch skipped | `--plex-db` for ratings; Tautulli/Tracearr for watch history |

The HTTP Home-user path (plex.tv switch, then `/api/resources` for a server `accessToken`) is attempted, then **discarded** if it is the admin token or if PMS returns the same stars as the admin. Importing that payload would copy the owner's ratings onto `plex-charlotte`.

### `--plex-db` (local Home ratings)

Copy from the Plex host (stop Plex, or copy all three files if Plex is running):

```
.../Plex Media Server/Plug-in Support/Databases/com.plexapp.plugins.library.db
com.plexapp.plugins.library.db-wal
com.plexapp.plugins.library.db-shm
```

Keep `-wal` / `-shm` next to the `.db` when they exist. Pass only the `.db` path. SQLite opens the siblings automatically.

The report prints `Plex DB ratings by account: 1 (Kaybi)=5131, 20281801=2245, ...`. Use those **SQLite** ids with `--users` / `--user-mapping`. They often differ from PMS `/accounts` ids (a current Home user can have 0 rows while an unnamed older `account_id` still holds the stars).

`--plex-db` does not replace `--source-url` / `--source-api-key`. The DB is the rating values; the API is still the catalog (titles, GUIDs, paths) and playlists.

Map names or ids onto K7 accounts with `--user-mapping` / `--auto-map-users` so data does not land on temp `plex-*` users.

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
3. **Title / identity** via bulk create (links to an existing indexed media when identity matches, otherwise creates virtual media unless `--only-match-existing`). Playlist imports use the same rule and log `matched (N virtual) / unmatched` per playlist.

Combined episode files (`S07E25-E26`): Partie 1 matches the first catalog episode. Partie 2 matches a later catalog episode **only when that number exists** in K7. If it does not (The Office Search Committee as one TMDb episode), Partie 2 is left unmatched so the file is not watched twice on E25, and no virtual E26 is created.

Series title identity also folds ` : ` vs `:`, comma vs colon subtitles (`90210 Beverly Hills : ...` / `90210 Beverly Hills, ...`), `/` vs fraction slash, trailing `.!?`, filler words (`Presents`), Japanese `ou`/`o` (Bungou / Bungo), country suffixes (`(US)`), FR/EN repeated-word titles (`Face to Face` / `Face a face`), and a trailing `(YYYY)` when that leaves a single series (so `Hunter x Hunter` maps to `Hunter x Hunter (2011)`, but `One Piece` does not bind to both the anime and `One Piece (2023)`). Shared nicknames (`Konosuba`) keep every prefixed series and pick the one that uniquely has that `SxxExx`, but a full title hit stays on that show even when another franchise series has the same `SxxExx` (Konosuba Explosion, Ranking of Kings : Le tresor du courage). Parent-series guids are ignored in that case so Tautulli cannot pull the match onto the main show. A single nickname hit is not enough (`DanMachi` must not bind to Sword Oratoria). An English (or localized) subtitle after ` - ` / ` : ` matches the full K7 title (`Daemons of the Shadow Realm` -> `Tsugai - Daemons of the Shadow Realm`). Distinctive last tokens (`Mayfair`) and a last-resort TMDb/TVDb search (existing K7 external id only) cover translated titles. Parent-series ids from Tautulli `get_metadata` resolve the show, then `SxxExx`. External IDs must match the same media kind: an episode item cannot attach to a series or movie.

Music title identity also folds curly vs ASCII apostrophes, hyphen vs space, `&` / `and`, and recording-edition suffixes (`Original Version`, remaster) while keeping Live / Remix / Acoustic distinct. Soundtrack artist credits can match an album prefix (`Arcane` vs `Arcane: League of Legends...`). Latin artist names match MusicBrainz sort names (`First Last` / `Last, First`) when K7 still shows the official native-script name. Same-title editions (different Spotify ids / ISRCs) collapse to one media: if any edition already matches K7, the whole group attaches there (K7 ISRC first, then a playable library file, then a virtual). Otherwise one virtual is created from the most popular Spotify track, all Spotify ids are kept, and only that popular ISRC is stored. A later library scan attaches a file to that virtual when the tags carry the same ISRC. Covers stay distinct (same title, different artist). Spotify export items can carry ISRC from the catalog API (bulk, when the token works) or from Odesli on leftover unmatched titles. K7 already stores ISRCs from MusicBrainz metadata refresh, so that is the reliable Spotify-to-library link (library tracks rarely have Spotify IDs).

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
| Created / would create virtual media | New lightweight media for unmatched source items (dry-run reports "would create" without writing) |
| Unmatched items | No match and virtual creation disabled or not applicable |

In `--dry-run`, the tool still resolves users (including temp accounts it *would* create), fetches source data, and matches against K7. The final report includes:

- **Users**: mapped / auto-mapped / would create temp / skipped
- **Media matching**: matched (by external id / path / title), would create virtual, unmatched
- **Per user**: history / ratings / playlists counts with matched vs unmatched
- **Playlists**: each playlist with source / matched / would create / unmatched (or skipped)
- **Full media lists**: every matched, would-create, and unmatched item with status, title, source id, K7 id when known, provider ids, and file paths

For large imports, pass `--report report.txt` (or `-o report.txt`): the complete lists go to the file, and the console keeps a short summary (first 30 of each media list).

Tip: use `--auto-map-users` or `--user-mapping Kaybi:kaybi` so dry-run shows real target accounts instead of temp `tracearr-*` users.

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
