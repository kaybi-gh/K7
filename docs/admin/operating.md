# Operating the server

Day-to-day administration after [install](install.md) and [configuration](configuration.md). End-user features: [Using K7](../user/guide.md).

## Memory and container sizing

K7 is a long-running .NET process. After a library scan or heavy work, Docker **memory used** (Working Set) can stay elevated even with **0 streams and 0 background tasks**. That is usually the runtime keeping pages for reuse, not a leak. Prefer Admin health **Memory used** vs **GC heap**: if the GC heap is clearly lower and stable across scans, treat sticky RSS as normal.

The published image defaults to **Workstation GC** (lower idle RAM for self-hosted). Large multi-user hosts can switch back with `DOTNET_gcServer=1`. The image also sets `MALLOC_TRIM_THRESHOLD_=131072` so native allocators (Skia / glibc) return memory more eagerly.

### Suggested `mem_limit` for `k7-server`

Size for the **peak** (scan, probe, optional software transcode), not the idle floor. A very high limit (for example 8g) gives the GC little reason to shrink the Working Set at rest.

| Profile | Limit | Typical fit |
|---|---|---|
| Small / trial | `1g` | Small library, direct play, little concurrency |
| Home | `2g` | Typical house: scan + metadata + 1-2 plays |
| Comfort | `3g`-`4g` | Large libraries, soft-transcode, several users |

Postgres is a separate container (~256-512 MiB extra).

### Concurrent streams (rough guide)

Playback peak depends on **how** clients play, not only on how many watch at once. Use these as order-of-magnitude add-ons on top of the base limit above. They are not a precise formula.

| Playback mode | Extra RAM per concurrent stream (order of magnitude) |
|---|---|
| Direct play / remux | Negligible for the K7 process |
| Hardware encode (VAAPI / NVENC) | About **0.1-0.3 GiB** process RAM (GPU memory is separate) |
| Software video transcode | About **0.5-1.5 GiB** depending on resolution and filters |

Examples:

- Home, mostly direct play, up to 2 clients: **`2g`** is usually enough.
- Home, 2 plays with 1 software transcode: aim for **`2g`-`3g`**.
- Four concurrent software transcodes: **`4g`+**.

Library scan and probe peaks are separate from stream count. Size for the worse of "large scan" and "max concurrent transcodes", not the sum of every worst case at once unless that can really happen together.

Optional: `DOTNET_GCConserveMemory=5` (values 1-9) makes the GC keep a smaller managed heap at the cost of more collections. Details: [.NET garbage collector config - Conserve memory](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/garbage-collector#conserve-memory).

Example Compose fragment:

```yaml
k7-server:
  mem_limit: 2g
  environment:
    DOTNET_GCConserveMemory: "5"   # optional
    # DOTNET_gcServer: "1"         # optional: Server GC on large hosts
```

Cold start at rest is often roughly **350-450 MiB**. After activity with a generous limit, **500-900 MiB** sticky RSS is common. Suspect a managed leak only if **GC heap** climbs unboundedly on every scan without plateauing.

## Libraries and media organization

Administrators create libraries in the admin UI and point each at a root folder visible inside the container (for example `/media/movies`).

1. Mount media read-only (recommended), e.g. `/media/movies`, `/media/series`, `/media/music`.
2. Create a library with a unique `RootPath` and the correct media type.
3. Optionally tune library processing (intro detection, seekbar thumbnails, chapter extraction, transmuxing/transcoding).
4. Run a scan / wait for background library-scan tasks.

**Realtime monitoring** (library setting): watches the root folder for adds/renames/deletes and queues a path-scoped scan after a short debounce. Each library uses one recursive watcher (one Linux inotify instance). It needs a filesystem that supports inotify (Linux) or equivalent (local ext4/xfs, typical Docker bind mounts to local disks). NFS, CIFS/SMB, and some FUSE/remote shares often cannot create watches - scans and auto-scan still work. K7 logs a Warning once with the failure reason, then retries quietly. Turn realtime monitoring off on those libraries and rely on **Auto scan interval**, or mount media the same way as a library that already shows `Started realtime monitor ...`. Huge local trees may still need a higher host `fs.inotify.max_user_watches`.

Access can be restricted per library and per profile beyond social visibility rules.

### Chapter extraction

For Movie and Serie libraries, **Chapter extraction** (enabled by default) stores embedded file chapters (MKV, etc.) on video file metadata at probe time. Users can show or hide seekbar chapter ticks under Settings -> Video playback (server default under Admin -> Video playback). When ticks are on, the seekbar also shows intro/outro markers from detected media segments if present; overlapping file chapters win over duplicate intro/outro ticks.

Files already indexed without chapters show as **Chapters not extracted** under Admin -> Diagnostics
(details, polish work class) when the library setting is on. Fix with **Extract chapters**, Fix all
from details, a polish shortcut on the diagnostics dashboard, or play the file once (lazy sync extract
on stream session).

### Explore feed

Category cards on Explore open a per-group feed (`/explore?library-group={id}`) by default, or library browse (`/library-groups/{id}`) when that group's tap action is Browse. Set the default per group in Admin -> Library groups (edit dialog). Users can override each group under Settings -> Libraries. Long-press or right-click on a card still offers both destinations.

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

**TV series:** Prefer `SxxExx` or `s01e01` (also `1x01`). Season folders: `Season 1`, `Saison 1`, `S01`, `Show Name S04`, `Specials`. Prefer standard episode naming when possible. Include the year in the series folder when there are homonyms, and optionally a provider id: `Show Name (2023) [tmdbid-123]`.

When the last indexed file for an episode is removed (scan or delete), K7 removes orphan episodes with no user data and cascades to empty seasons and series. Episodes kept for watch history, playlists, or collections are retained without files until you clear that state. Library browse, home, and search only list series/seasons/episodes that still have playable files. Series detail carousels hide empty seasons and non-playable episodes. History, reviews, and stats deep links can still open those media pages (playback is disabled with an unavailable state).

When a directory already has episodes attached to a single series, new files in that folder are attached to the same series (folder consensus). Close title variants parsed from filenames in the same folder are also unified before matching. A mis-matched episode file can be re-identified from the episode page (Indexed versions).

Adding a new episode to an existing series refreshes series-level fields (status, overview) and enriches the new episode (and its season when that season is new). Existing episodes keep their metadata and artwork. A manual **Refresh metadata** or the scheduled library refresh still re-fetches the whole series.

```text
/media/series/Show Name/
  Season 01/
    Show Name - S01E01 - Pilot.mkv
```

**Music:** `Artist/Album/01 - Track.ext` or `Album/01 - Track.ext`. Leading track numbers are recognized.

### Metadata providers

| Provider | Used for | Admin API key? |
|---|---|---|
| Auto (default for series) | Series identity + numbering | No - picks TMDb/TVDB per show |
| TMDb | Movies, series (forced or Auto) | No - bundled in the server |
| TheTVDB | Series (forced or Auto) | No - bundled in the server |
| MusicBrainz / Cover Art Archive | Music | No API key. Polite User-Agent only |

**Music identification.** K7 prefers MusicBrainz ids embedded in audio tags (Picard / TagLib: release-group, release, artist, album artist, recording) before searching. Album search uses release + artist (or `arid:` when an artist MBID is known). Tag **year is not a hard Lucene filter** - a wrong year no longer zeros all hits. Year only re-ranks candidates. Lucene special characters in titles/artists are escaped. After Rematch (or first ingest), album refresh propagates artist MBIDs from release credits with normalized name matching.

**MusicBrainz display language.** MusicBrainz official names are often in the artist's native script. K7 uses the library metadata language (default `fr`, fallback `en`) to pick a localized alias when one exists, and otherwise unfolds a Latin sort name (`Last, First` -> `First Last`). The official name is stored as `OriginalTitle`. Refresh existing albums/artists after this change to rewrite titles already saved in a non-Latin script.

**Series display language.** TMDb and TheTVDB often return the original-language title/overview (Japanese for anime) when the library language has no translation. K7 then uses the metadata fallback language (default `en`) when that text is still in a different script. Refresh existing series after this change.

**Series libraries default to Auto.** Auto scores TVDB and TMDb search hits together (title, year, popularity), stores both external ids when known, then picks the **numbering canon** (which S/E grid matches your files best). The other provider still enriches gaps (stills, ratings, cast). Force **TMDb** or **TheTVDB** only when you need a fixed numbering scheme (for example Sonarr/TVDB-named packs).

Optional folder tokens improve matching: `Series Name (2023) [tmdbid-123]` or `[tvdbid-456]` / `[imdbid-tt...]`.

Field locks in the UI prevent refreshes from overwriting manual edits. Artwork lives under `Paths:Metadatas` - recommended in backups (regenerable via metadata refresh, but slow).

**Rematch metadata (Admin -> Libraries):** after improving matching (or switching a series library to Auto), use the magnifying-glass action on a **local** library. It queues a background task (same pattern as library index): pending/waiting-for-retry CreateMedia tasks for that library's files are cancelled first (so a prior scan cannot recreate duplicates after rematch), files are detached from media, then CreateMedia work is enqueued per movie/album/series. Progress appears under Admin -> Background tasks. It re-runs identification with the current algorithm and only re-links a file when the resolved identity differs. Probes and HLS stay on the files. Watch progress, playlists, collections, playback history, and exclusions transfer to the corrected media when a file is re-linked. For music, K7 prefers keeping the same track/album Guids when the corrected ExternalId is free (AudioMuse / OpenSubsonic ids stay stable). Merges onto an already-existing identity delete orphan Guids and then debounce an AudioMuse align + clean. Federated libraries cannot be rematched this way. Prefer rematch over delete/recreate when the library settings and file probes are still good. One failed CreateMedia (for example a single album) is retried independently and does not stop the rest of the library.

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

Size the transcoding volume for concurrent streams. Safe to wipe between runs (cache). Idle jobs are removed after about 1 hour.

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

Rebuild/pull an image that includes VAAPI drivers (`va-driver-all`, plus Intel non-free when available on amd64). The stock K7 image installs these; older builds with only `ffmpeg` will not encode even with `/dev/dri` mounted.

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
4. On accept: OpenIddict peer credentials, share agreements, library discovery. Optional **Automatically share new libraries** stores `AutoAddNewLibraries` on the peer.
5. To disconnect: revoke / delete the peer (best-effort notify + local cleanup).

### Library sharing

Outbound share agreements control which local libraries a peer can list via `GET /api/federation/libraries`.

- **Manual**: peer settings -> shared libraries.
- **Auto-share**: with `AutoAddNewLibraries` enabled on a peer, creating a **local** library creates an outbound agreement for that peer and best-effort `share-update` notify when outbound credentials exist. The consumer syncs (webhook, scheduled sync, or Admin -> Sync) and, with the same flag, enables the new inbound library automatically.

Back up `Paths:Config` - federation identity material lives with OpenIddict keys. User-level share/view scopes are separate - see [Using K7 - Privacy](../user/guide.md#privacy-and-visibility).

Local testing: [`docker-compose.federation-test.yaml`](../../docker-compose.federation-test.yaml).

## Administration UI

### Dashboard and diagnostics

Admin Dashboard KPIs and Libraries issue chips link into **Admin -> Diagnostics**.

- **`/admin/diagnostics` (dashboard):** media-health overview for K7 - severity totals, work-class
  issue cards (with Fix all when a bulk fix exists), see-all count links, and per-library
  Error/Warning/Info breakdown. Clicks deep-link into details. On TV, work-class issue cards are
  omitted to keep the WebView lighter. Use **View all issues** or the per-library table.
- **`/admin/diagnostics/details`:** work queue - filterable table, per-row fixes, re-identify, and
  **Fix all** when a single fixable issue filter is active. Use the toolbar hint if Fix all is disabled.
- **Bulk fix confirm dialog:** states what will be queued and realistic outcomes (including that
  re-identify / refresh / create-media may find no match and leave some items unchanged).

On TV (Android TV / native TV clients), Admin Dashboard skips live charts and metrics polling.
Current CPU / RAM / network stay as numbers, active streams stay live, and background-task counts
refresh on a longer debounce. Background tasks itself still live-updates, but at most about every
10 seconds.

Severity (Error / Warning / Info) classifies findings - not a promise that a fix or refresh will
succeed. Work class is the nature of the work:

| Work class | FR | Examples |
|---|---|---|
| Catalog | Catalogue | Unlinked file (ex orphan/unidentified), missing file metadata, inaccessible path, duplicates |
| Enrichment | Enrichissement | Missing external id, metadata, pictures, stale metadata, missing artist members |
| Polish | Finition | HLS segments, chapters, theme song, intro/outro, audio analysis |

**Severity bands (surfaced):**
- **Error:** unlinked file, missing file metadata, missing external id, inaccessible library path
- **Warning:** duplicate external id
- **Info:** missing metadata / pictures / members, stale metadata, polish issues, suspected duplicates

**Unlinked file** (`OrphanFile`): one surfaced issue for files with no media link (`MediaId` is null).
Always **Error**. Includes unidentified files (filename parse failed) while they remain unlinked.
Re-identify when identification is missing, Retry create media / Fix all when identification exists.
After a successful re-identify the file is linked and drops off even if filename identification is
still missing. `UnidentifiedFile` is kept for API compat but is not shown separately.

**Missing members** is music artists with no person credits (`PersonRoles`), not missing episodes or tracks.

**Missing files on a media** is not diagnosed: medias may intentionally remain without files for watch
history, ratings, and stats.

In-app **?** help on the diagnostics dashboard and details lists each surfaced issue, definition,
applies-to scope, and what bulk fix will attempt.

Admin Dashboard still shows a short health overview and active streams (encoder / hardware vs software).

### Users and authentication

- Activate Guest, roles, profile restrictions, and per-user capability overrides under Admin -> Users.
- Restriction profiles hide matching library titles from browse, search, Home, and person filmography. Person **Known for** (external posters not in the library) is hidden entirely while a restriction profile is active, including on a shared profile.
- Guest playback is recorded in Admin -> Playback history (and active streams) without giving Guest continue watching or personal history.
- Admin -> Playback history can reassign a play (personal or any shared profile) and delete rows. A personal delete updates that user's play/skip counts. A shared-profile delete removes the row for the whole group without un-watching members. Bulk select works like Admin -> Users. Delete requires `CanDeleteHistory`. Reassign requires `CanReassignHistory`.
- `CanDeleteHistory` and `CanReassignHistory` are on by default for Administrator only (off for User and Guest). Enable them per User account when needed. Turning them off on an admin keeps history visible while blocking those actions.
- Authentication panel: **read-only** view of local / OIDC flags from config.

### Server defaults vs user overrides

Almost all personalization has server defaults (e.g. `/admin/video-playback`) and per-user overrides under `/settings/...`. Users can reset to defaults from the settings action bar.

### Background tasks

- List / cancel / summary: `/api/background-tasks`
- Settings: `GET/PUT /api/admin/background-tasks/settings` (worker count default 3, `0` pauses all
  workers; per-lane concurrency including Metadata ceiling, `0` pauses that lane)
- Library scans use the `library-scan` concurrency group (default limit 1). Workers reserve a group slot before claiming a task so the configured limit is not bypassed under parallel dequeue.
- In-app **?** help on Admin -> Background tasks summarizes work classes, lanes, metadata-provider
  limits, and the main task types (cheat sheet; this section remains the deep reference).

#### Lanes and time-to-usable

Scheduling has three axes, deliberately separated:

- **Lane** answers *which local resource does this saturate*. It is the only axis an operator
  configures, because it is the only one that maps onto hardware.
- **Work class** answers *what does this contribute to, and at which stage*. It is product policy, fixed
  in code and not configurable.
- **Priority** carries dynamic urgency (a user action, or a media someone just asked to play).

Selection order is work class descending, then priority descending, then creation date. Work class
values are the scheduling weights themselves, so the order is served directly by
`IX_BackgroundTasks_Status_WorkClass_Priority_Created`.

There is no time-based aging: deferring polish while critical work remains is intended, and once a scan
drains there is nothing critical left. Creation date as the last key keeps the order fair inside a class.

| Lane | Default | Work |
|---|---|---|
| `Probe` | 4 | `ffprobe` container reads: file metadata and chapter extraction. IO seek bound, safe to parallelize. |
| `LibraryScan` | 1 | Filesystem indexing. |
| `FfmpegPrepare` | 1 | Keyframe extraction for transmuxing, stream preparation. CPU bound. |
| `MediaAnalysis` | 1 | Intro/outro detection, audio analysis, theme song extraction. |
| `ImageExtract` | 1 | Seekbar thumbnails and stills extracted with ffmpeg. |
| `ImageProcessing` | 2 | Local image variant generation. |
| `Metadata` | 8 | Identification, metadata refresh, provider downloads. Ceiling across external providers (1 task each). |
| `Federation` | 1 | Peer synchronization, isolated per peer. |
| `DownloadTranscode` | 1 | Transcoding when preparing offline downloads that cannot direct-play. |

Setting a lane to `0` pauses that category, which is useful to stop polish during a large import.

| Work class | Meaning |
|---|---|
| `CriticalProbe` | Container probe: makes a media playable. Ranks **above** `CriticalLink`. |
| `CriticalLink` | Media creation and file linking: makes a media visible and navigable. |
| `CriticalEnrich` | Metadata refresh and main poster: makes a visible media presentable. |
| `Prepare` | Keyframes and other work that only improves an already playable media. |
| `Polish` | Thumbnails, intro detection, image variants, content hashing. |

Why `Probe` is separate and parallel: playback negotiation needs the container and codecs of a file, so a
media stays unplayable until its probe has run. Probes used to share a single `ffmpeg` group limited to 1
and were therefore serialized behind transcoding, which made a first scan of a large library probe files
one at a time. Keeping `Probe` at or above the worker count also means the probes enqueued for a scan
batch drain before the media creation tasks of the same batch obtain a worker, so a media is normally
already playable by the time it becomes visible.

Probing ranks above linking on purpose: the probes of a scan batch drain before the media creation tasks
of the same batch obtain a worker, so a media is normally already playable by the time it becomes visible.
The reverse order lets a media appear while its probe is still queued, which is the ghost-page state this
scheduling exists to avoid. The trade-off is accepted knowingly: the first media appears slightly later,
and everything that appears is real.

During a scan, both probe and media creation tasks are enqueued as their files get persisted, rather than
in a single batch at the end, so the queue works while the scan is still walking the filesystem.
Identification still runs once over the whole library so grouping stays correct - an album or a serie
spans several save batches and must yield a single task - and a media creation task is only released once
every file it groups has been saved.

#### Duplicate media protection

Creating a media is a check-then-insert: look it up by external id, then by title, then insert. Nothing in
the database prevents a duplicate, since `Medias` has no unique constraint on identity and `ExternalIds`
is indexed but not unique. Two commands resolving to the same album, serie or movie can legitimately be
queued at once (two scan batches, a watcher flush racing a scheduled scan, two files of one movie), and
running concurrently on the `Metadata` lane they would both miss the lookup and both insert. K7 has no
merge tooling, so a duplicate media is a manual cleanup.

Media creation is therefore serialized per media identity, on a key built from the library, the media type
and the same criteria the lookup uses (album plus artist plus year, serie title, movie title plus year).
Like the concurrency gate, this lock is held in-process, consistent with running a single instance.

The lock reduces the probability of a duplicate; it cannot remove it, because two different identity keys
can designate the same media (a renamed folder, a file named differently, a provider changing its mind).
Two read-only diagnostics therefore report duplicates instead of trying to prevent them, under
Admin -> Diagnostics:

| Diagnostic | Severity | Signal |
|---|---|---|
| Duplicate (shared external id) | Warning | Two medias share the same provider id. Reliable. A local media and its federated copy may legitimately share one, so only medias with the same peer are compared. |
| Suspected duplicate (same title and year) | Info | Two medias of one type share a normalized title and release year in one library. Noisier: homonym movies do exist. |

Detection only: neither offers a fix action. Merging two medias means re-pointing indexed files, playback
progress, playlist entries, ratings, reviews, collections, external ids and artwork, and deciding what to do
with conflicting progress - a feature of its own, not yet implemented.

Provider concurrency on the Metadata lane is fixed at **one in-flight task per metadata
provider** (`tmdb`, `tvdb`, `auto`, `musicbrainz`, `wikidata`, `wikimedia`, `coverart`, `local`). HTTP pacing
stays on the outbound rate limiter (MusicBrainz 1.1s, Wikidata/Wikipedia 1s, and so on). The Metadata
lane limit is the **ceiling across providers** (default 8): how many different providers may run at
once. Set it to `0` to pause all Metadata work. The admin settings dialog lists each provider with
active/pending counts. The per-provider limit is not editable.

A provider HTTP **429** also starts an **admission cooldown** until the `Retry-After` instant: workers
skip that provider (spill over to other work) instead of launching tasks that would fail until the
window ends. The task that hit 429 is scheduled with `NextRetryAfter` aligned to that delay and a
priority boost so it is preferred when the cooldown lifts. The settings UI shows the cooldown end
time while it is active.

Workers prefer higher work classes, but **spill over** to eligible work on unsaturated lanes or
providers when the preferred head of the queue cannot acquire a slot, so idle workers do not wait
behind a saturated CriticalEnrich Metadata backlog while Polish or Probe work is available.

#### Provenance

Each task records who created it: `User`, `Scheduler`, `Watcher`, `System`, `Federation` or
`Diagnostics`. This is observability, not priority; the scheduler never orders on it. It does set the
initial priority, so an explicit user action starts ahead of a backlog. The task list can be
filtered by provenance.

#### On-demand boost

Asking to play a media that has not been probed yet returns 422 with the
`https://k7.media/problems/media-not-ready` problem type, an `indexedFileId` extension member,
and raises the priority of that media's pending tasks, then wakes the workers. The update is
scoped to one or two target entity ids and backed by `IX_BackgroundTasks_TargetEntityId`. Scores are
otherwise only ever set at enqueue time: a broad re-scoring pass would churn the scheduling index,
contend with the scan writer on SQLite and risk update-update deadlocks on Postgres.

Clients are told when a probe completes (`ReceiveMediaIndexedFilesUpdated`), so a page showing "being
prepared" recovers on its own.

#### Reliability

- Enqueue deduplication is atomic, enforced by a unique filtered index on
  `(Name, TargetEntityId)` over the active statuses. A watcher event and a scheduled scan racing on the
  same media now resolve to one task instead of two.
- Retry backoff is exponential (30s doubling, capped at 15 minutes) **with full jitter**, so tasks that
  failed together during a provider outage do not retry in a synchronized burst.
- An `InProgress` row past its timeout with no worker in this process is reclaimed to `Pending`, and the
  reclaim is **counted**. After 3 reclaims without completing, the task is failed instead of requeued:
  a task that kills the process (an OOM during ffmpeg) used to loop forever without ever incrementing
  its attempt count.
- Cancelling a running task now signals the handler through its cancellation token instead of only
  writing a status while the work kept running and held its lane slot. A cancellation asked for by an
  operator is reported as cancelled, not as a timeout.

#### Single instance

Concurrency counters and the cancellation registry live in the process. **K7 must run as a single
instance against a given database.** Two instances would each enforce the limits locally (doubling
every lane), and each would reclaim the other's in-flight tasks as orphans. Supporting several instances
would mean moving the counters into the database; Postgres advisory locks would serve, but SQLite has no
equivalent, so it would be a Postgres-only capability.

#### Upgrading from concurrency groups

The `BackgroundTaskConcurrencyLimits` setting (a free-form dictionary keyed by group name) is replaced by
`BackgroundTaskLaneLimits`, keyed by lane. **Previously configured limits are not carried over**; re-apply
them on the corresponding lanes. The former `file-metadata` and `ffprobe` groups are both folded into
`Probe`, and per-provider groups (`tmdb`, `musicbrainz`, ...) into `Metadata`.

The migration backfills existing rows: lane from the old group name, work class from the task name (a
better signal than the old priority, which mixed kind of work and urgency), and provenance defaults to
`System`. Duplicate active tasks are removed before the unique index is created, keeping the oldest of
each set.

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

1. In K7, create an API key under **Admin -> API keys** (admin only).
   - API keys are admin/automation credentials. They are not assigned to a user account.
   - **Read** is enough when K7 drives Music intelligence (K7 asks AudioMuse for track ids and creates playlists itself).
   - **Write** is required if AudioMuse must create or update playlists on K7 (analysis write-back).
2. In AudioMuse, add a media server of type **Navidrome** (OpenSubsonic-compatible) pointed at the K7 base URL (default HTTP port **7080**). Use the K7 API key in the OpenSubsonic **API key** field (AudioMuse sends `apiKey=` and must not send `u`). Leave username/password empty when using an API key. Prefer HTTPS in production (credentials travel in query strings). Requires AudioMuse-AI with OpenSubsonic `apiKey` auth support.
3. In K7: Admin -> Music intelligence (`/admin/music-intelligence`) - enable, set AudioMuse base URL, optionally an AudioMuse API token if AudioMuse auth is enabled, then test connection (stored as `AudioMuseAi`).

K7 talks to AudioMuse over AudioMuse's own HTTP API. AudioMuse talks back to K7 over the OpenSubsonic `/rest` facade (same Guid media ids as `/api`).

After music Guids are deleted or replaced (merge rematch / reidentify onto another album, orphan track cleanup after file delete), K7 debounces a best-effort AudioMuse **server align** then **catalogue cleaning**. Align remaps server item ids when fingerprints still match; cleaning drops mappings that no longer exist on any server. Pure in-place ExternalId corrections keep the same Guids and do not trigger reconcile.

When disabled, AI discovery stays hidden; basic radios still work. User features: [Using K7 - Music discovery](../user/guide.md#music-discovery-audiomuse).

### Import from other servers

[tools/K7.Import/README.md](../../tools/K7.Import/README.md) - import from other libraries and services. Back up the database first. There is no import undo (see [Backup and troubleshooting](backup-and-troubleshooting.md)).
