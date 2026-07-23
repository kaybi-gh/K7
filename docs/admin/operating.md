# Operating the server

Day-to-day administration after [install](install.md) and [configuration](configuration.md). End-user features: [Using K7](../user/guide.md).

## Libraries and media organization

Administrators create libraries in the admin UI and point each at a root folder visible inside the container (for example `/media/movies`).

1. Mount media read-only (recommended), e.g. `/media/movies`, `/media/series`, `/media/music`.
2. Create a library with a unique `RootPath` and the correct media type.
3. Optionally tune library processing (intro detection, seekbar thumbnails, chapter extraction, transmuxing/transcoding).
4. Run a scan / wait for background library-scan tasks.

Access can be restricted per library and per profile beyond social visibility rules.

### Chapter extraction

For Movie and Serie libraries, **Chapter extraction** (enabled by default) stores embedded file chapters (MKV, etc.) on video file metadata at probe time. Users can show or hide seekbar chapter ticks under Settings -> Video playback (server default under Admin -> Video playback). When ticks are on, the seekbar also shows intro/outro markers from detected media segments if present; overlapping file chapters win over duplicate intro/outro ticks.

Files already indexed without chapters show as **Chapters not extracted** in Admin diagnostics when the library setting is on. Fix with **Extract chapters**, or play the file once (lazy sync extract on stream session).

### Theme songs

Detail pages can play an ambient theme when a file is available (user toggle: Settings -> Experience -> General -> enable theme songs; optional per-device disable on the same page. Server default: Admin -> Experience -> General). Theme continues across related series pages (serie / season / episode) and cast person digressions; a finished theme does not restart on return to the same media. Leaving that media context fades out, and opening another media with a theme crossfades.

- **Library sidecar (read-only):** `theme.mp3` / `.flac` / `.m4a` / `.ogg` at the series root (next to season folders) or in the movie folder. For movies, same-basename audio is also accepted (for example `Movie Name (2020).mkv` + `Movie Name (2020).mp3`). Sidecars are never written by K7.
- **Series auto-extract:** when **theme song generation** and **intro/outro detection** are enabled on the library, and no sidecar exists, K7 may extract a faded MP3 from an Intro segment into `Metadatas/medias/{serieId}/theme.mp3`. Movies are not extracted from video; sidecar only.
- Existing metadata themes still play if generation or intro detection is later turned off; new extracts do not run.
- **Theme song not generated** (warning): series with generation on, at least one detection-eligible season (2+ episodes with files), and no theme file. Fix with **Generate theme song** (extracts if an Intro already exists; otherwise queues intro/outro detection on eligible seasons, which then queues theme extract).
- **Intro/outro missing** (warning): episodes in intro-detection-enabled libraries whose season is eligible and that have neither Intro nor Outro segments. Fix with **Detect intros/outros** (queues season-level detection; multiple episodes in the same season share one background task). Re-running detection also queues theme extract when intros are found and theme generation is on.

### Folder and naming conventions

The scanner derives titles from filenames and folders. Prefer consistent layouts:

**Movies:** `Movie Name (2019).mkv` or `Movie Name (2019)/Movie Name (2019).mkv`. Year helps matching; rip/quality tags are stripped when parsing.

**TV series:** Prefer `SxxExx` or `s01e01` (also `1x01`). Season folders: `Season 1`, `Saison 1`, `S01`, `Specials`. Prefer standard episode naming when possible.

When a directory already has episodes attached to a single series, new files in that folder are attached to the same series (folder consensus). Close title variants parsed from filenames in the same folder are also unified before matching. A mis-matched episode file can be re-identified from the episode page (Indexed versions).

```text
/media/series/Show Name/
  Season 01/
    Show Name - S01E01 - Pilot.mkv
```

**Music:** `Artist/Album/01 - Track.ext` or `Album/01 - Track.ext`. Leading track numbers are recognized.

### Metadata providers

| Provider | Used for | Admin API key? |
|---|---|---|
| TMDb | Movies | No - bundled in the server |
| TheTVDB | Series | No - bundled in the server |
| MusicBrainz / Cover Art Archive | Music | No API key; polite User-Agent only |

Series libraries use TheTVDB as the primary provider. When a TMDb (or IMDb) external id is available on the series, K7 also pulls **TMDb community ratings** (series and episodes) and prefers **TMDb episode stills** during metadata refresh. Cast is enriched the same way: match TVDB roles to the supplemental TMDb cast when possible, then resolve remaining TVDB people ids via TheTVDB people `remoteIds` (tmdb/imdb) and queue a TMDb person refresh only for still-thin profiles.

Field locks in the UI prevent refreshes from overwriting manual edits. Artwork lives under `Paths:Metadatas` - recommended in backups (regenerable via metadata refresh, but slow).

After federation peering, remote libraries can appear according to share agreements - see [Federation](#federation).

## Transcoding

K7 uses **ffmpeg** for on-the-fly transcoding and HLS when the client or network cannot play the original.

| Deploy | ffmpeg |
|---|---|
| Official Docker image | Installed in the image (`PATH`) |
| Custom / local run | Install ffmpeg/ffprobe, or set `Paths:FFMpegBinaryFolder` |

| Setting | Role |
|---|---|
| `Paths:FFMpegBinaryFolder` | Optional directory override |
| `Paths:Transcoding` | Working directory for segments / temp files |

Size the transcoding volume for concurrent streams. Safe to wipe between runs (cache).

### Hardware acceleration

The server probes ffmpeg once per process lifetime (in-memory cache), then **verifies** each candidate hardware encoder with a short encode test. Only encoders that actually work are listed under Admin -> Transcoding. Built-in ffmpeg encoder names (for example `h264_nvenc` on Ubuntu packages) are **not** enough - the GPU device and drivers must be reachable inside the container. Failed probes (for example NVENC without a GPU) are summarized once at Information without dumping full ffmpeg stderr; details stay at Debug.

Supported families:

- NVIDIA: `h264_nvenc`, `hevc_nvenc`
- Intel Quick Sync: `h264_qsv`, `hevc_qsv`
- VAAPI (Intel/AMD via `/dev/dri`): `h264_vaapi`, `hevc_vaapi`
- Also: VideoToolbox, AMF
- Software fallback: `libx264` / `libx265`

Controlled in **Admin -> Transcoding** (server setting `TranscodeSettings`), not `appsettings.json`:

- Hardware encoder mode: Auto / Software / HardwarePreferred
- HDR tonemap and concurrency / quota options
- APIs: `/api/admin/transcode/settings`, `/capabilities`, `/test`

Use **Test encoder** after changing Compose devices. If verification finds no working hardware encoder, Auto falls back to software.

#### Docker Compose device passthrough

The stock [`docker-compose.yaml`](../../docker-compose.yaml) does **not** pass through GPUs. Add one of the following to the `k7-server` service, then recreate the container and confirm Admin -> Transcoding.

**Intel / AMD (VAAPI)** - mount DRM render nodes:

```yaml
devices:
  - /dev/dri:/dev/dri
```

Rebuild/pull an image that includes VAAPI drivers (`mesa-va-drivers`, `intel-media-va-driver`, ...). The stock K7 image installs these; older builds with only `ffmpeg` will not encode even with `/dev/dri` mounted.

The entrypoint adds `appuser` to the GIDs that own `/dev/dri/renderD*` / `card*` so you usually do **not** need Compose `group_add`. If encode probes still fail with permission errors, add the host `video` / `render` GIDs explicitly:

```yaml
# group_add:
#   - "44"    # video (example - check `getent group video` on the host)
#   - "992"   # render (example - check `getent group render` on the host)
```

`/dev/dri` does **not** enable NVIDIA NVENC. It only exposes Intel/AMD DRM devices for VAAPI (and often QSV on Intel).

K7 initializes VAAPI with `-init_hw_device vaapi=va:/dev/dri/renderD*` before the input, then `format=nv12,hwupload` + `h264_vaapi` / `hevc_vaapi`. If Admin still shows no hardware encoders after mounting `/dev/dri`, check container logs for `Hardware encoder probe complete` / Debug verification lines and run `vainfo` inside the container.

PGS subtitle burn-in still does the overlay on the CPU (`scale2ref` / `overlay`). When VAAPI (or another encoder that sets a post-overlay `-vf`) is selected, K7 appends that filter inside the same `-filter_complex` (for VAAPI: `format=nv12,hwupload`) so encode can stay on the GPU. Decode stays software for burn-in so the overlay filters keep system-memory frames.

**NVIDIA (NVENC)** - NVIDIA Container Toolkit on the host, then either:

```yaml
gpus: all
# or, Compose deploy form:
# deploy:
#   resources:
#     reservations:
#       devices:
#         - driver: nvidia
#           count: 1
#           capabilities: [gpu]
```

After recreate: open Admin -> Transcoding, confirm detected hardware encoders lists only working encoders, and run **Test encoder**.

Users pick stream quality in the player; that drives whether a remux/transcode session is needed.

## Federation

Federation links two K7 instances so friends can share and stream remote media without duplicating files.

### Prerequisites

1. Feature flag **Federation** enabled (Admin; **disabled by default**).
2. `BaseUrl` on **both** servers reachable by the peer (HTTPS recommended).
3. Optional `Server:Name` for invitations.
4. Network path open between peers.
5. For HTTP LAN peers, see `Security:Federation:*` in [configuration.md](configuration.md#security).

### Peering flow

1. Requester admin: Admin -> Federation -> request peering with the remote base URL.
2. K7 POSTs to the remote peer-request endpoint and stores a pending peer + token.
3. Remote admin accepts or rejects.
4. On accept: OpenIddict peer credentials, share agreements, library discovery.
5. To disconnect: revoke / delete the peer (best-effort notify + local cleanup).

Back up `Paths:Config` - federation identity material lives with OpenIddict keys. User-level share/view scopes are separate - see [Using K7 - Privacy](../user/guide.md#privacy-and-visibility).

Local testing: [`docker-compose.federation-test.yaml`](../../docker-compose.federation-test.yaml).

## Administration UI

### Dashboard and diagnostics

Health overview and active streams (encoder / hardware vs software for the current decision).

### Users and authentication

- Activate Guest, roles, profile restrictions under Admin -> Users.
- Authentication panel: **read-only** view of local / OIDC flags from config.

### Server defaults vs user overrides

Almost all personalization has server defaults (e.g. `/admin/video-playback`) and per-user overrides under `/settings/...`. Users can reset to defaults from the settings action bar.

### Background tasks

- List / cancel / summary: `/api/background-tasks`
- Settings: `GET/PUT /api/admin/background-tasks/settings` (worker count default 3; per-group concurrency for metadata, `ffmpeg`, `library-scan`, federation, etc.)
- Library scans use the `library-scan` concurrency group (default limit 1). Workers reserve a group slot before claiming a task so the configured limit is not bypassed under parallel dequeue.
- Task timeouts abandon the in-flight handler when sync filesystem I/O ignores cancellation, so the worker slot is released and the task is marked failed/retry instead of staying zombie `InProgress`.
- Realtime folder monitoring and path scans ignore NAS/system folders: `@eaDir`, `.@__thumb`, `@tmp`, `#recycle`, `@Recycle`, `.synology`, `.Trash-*`.
- After upgrading, restart the server once so any existing zombie `InProgress` scans are recovered (`RecoverStuckTasksAsync`), then re-run indexing if needed.

### Outgoing notifications (webhooks)

Outbound HTTP webhooks only (event filters + payload templates). CRUD + test: `/api/notifications/rules`.

Event catalog covers Playback, Library, Media, Playlist, Device, Download, Federation, and Health
categories. Notable Federation / Health events for ops monitoring:

| Event | Category | Fires when |
|---|---|---|
| `PeerConnectivityChangedEvent` | Federation | A peer test (scheduled or manual) transitions success/failure state, e.g. a peer goes offline or comes back |
| `TranscodeFailedEvent` | Health | An on-the-fly transcode/remux session fails for a media file |
| `MusicIntelligenceUnavailableEvent` | Health | AudioMuse AI is enabled but unreachable during a health probe |
| `LibraryScanCompletedEvent` | Library | A full or partial (path-scoped) library scan finishes, with added/skipped/inaccessible counts |
| `MediaCreatedEvent` | Media | A new media item is created from indexing |

### Music intelligence (AudioMuse AI)

Optional self-hosted [AudioMuse AI](https://github.com/NeptuneHub/AudioMuse-AI):

- Admin -> Music intelligence (`/admin/music-intelligence`)
- Settings: enabled, base URL, API key; test connection (stored as `AudioMuseAi`)

When disabled, AI discovery stays hidden; basic radios still work. User features: [Using K7 - Music discovery](../user/guide.md#music-discovery-audiomuse).

### Import from other servers

[tools/K7.Import/README.md](../../tools/K7.Import/README.md) - Plex, Jellyfin, Spotify, and more.
