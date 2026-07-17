# Operating the server

Day-to-day administration after [install](install.md) and [configuration](configuration.md). End-user features: [Using K7](../user/guide.md).

## Libraries and media organization

Administrators create libraries in the admin UI and point each at a root folder visible inside the container (for example `/media/movies`).

1. Mount media read-only (recommended), e.g. `/media/movies`, `/media/series`, `/media/music`.
2. Create a library with a unique `RootPath` and the correct media type.
3. Run a scan / wait for background library-scan tasks.

Access can be restricted per library and per profile beyond social visibility rules.

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

The server probes ffmpeg and can use:

- NVIDIA: `h264_nvenc`, `hevc_nvenc`
- Intel Quick Sync: `h264_qsv`, `hevc_qsv`
- VAAPI: `h264_vaapi`, `hevc_vaapi`
- Also: VideoToolbox, AMF
- Software fallback: `libx264` / `libx265`

Controlled in **Admin -> Transcoding** (server setting `TranscodeSettings`), not `appsettings.json`:

- Hardware encoder mode: Auto / Software / HardwarePreferred
- HDR tonemap and concurrency / quota options
- APIs: `/api/admin/transcode/settings`, `/capabilities`, `/test`

The stock Compose file does **not** pass through GPUs. Add device/runtime flags, confirm capabilities in Admin, then prefer HardwarePreferred or Auto. If no hardware encoder is visible, K7 falls back to software.

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

### Outgoing notifications (webhooks)

Outbound HTTP webhooks only (event filters + payload templates). CRUD + test: `/api/notifications/rules`.

### Music intelligence (AudioMuse AI)

Optional self-hosted [AudioMuse AI](https://github.com/NeptuneHub/AudioMuse-AI):

- Admin -> Music intelligence (`/admin/music-intelligence`)
- Settings: enabled, base URL, API key; test connection (stored as `AudioMuseAi`)

When disabled, AI discovery stays hidden; basic radios still work. User features: [Using K7 - Music discovery](../user/guide.md#music-discovery-audiomuse).

### Import from other servers

[tools/K7.Import/README.md](../../tools/K7.Import/README.md) - Plex, Jellyfin, Spotify, and more.
