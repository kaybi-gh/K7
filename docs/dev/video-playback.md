# Video playback (clients)

How first-party clients play video, and why the MAUI hosts differ by platform.

## MAUI layout during play

| Platform | Decode surface | Controls |
|---|---|---|
| Android / iOS | CommunityToolkit `MediaElement` (ExoPlayer on Android, **TextureView**) | Native XAML `NativeVideoPlayerOverlay` |
| Windows | Video.js inside WebView2 | Full Blazor `VideoPlayerControlsOverlay` (same as Web) |
| Web (WASM) | Video.js | Blazor `VideoPlayerControlsOverlay` |

Browse / library UI stays Blazor Hybrid. On Android/iOS, when `IPlayerService.IsVisible` is true, MAUI hides the BlazorWebView and shows MediaElement + native XAML chrome. Windows keeps the WebView2 + Blazor player UI because decode is already Video.js. Control plane remains [`IPlayerService`](../../src/Clients/Shared/Interfaces/IPlayerService.cs).

`MauiNativeVideoChrome.IsEnabled` is set at MAUI startup and is **false on Windows**.

Android keeps `AndroidViewType=TextureView` so the XAML overlay composites above the picture. `SurfaceView` ignores normal Z-order and can hide sibling overlays.

Offline / local files (`file://` or a filesystem path from the download store) use MediaElement `FromFile`. After assigning `Source`, Android rebinds ExoPlayer with `DefaultHttpDataSource` and longer timeouts so HLS `init.m4s` can wait on ffmpeg. That rebind is skipped for local files, otherwise playback never starts. `StreamUriService` builds a `file://` URI for offline sessions (`new Uri(androidPath)` throws `UriFormatException` because the path has no scheme).

`NativeVideoPlayerOverlay` (`src/Clients/MAUI/Controls/Video/`) targets 1:1 parity with the Blazor
`VideoPlayerControlsOverlay`: transport, seek bar with chapter ticks/sprite thumbnail preview and
hovered chapter title, playback settings (audio/subtitles/quality/speed/aspect, with TV D-pad
focus navigation. Audio and subtitle labels are the normalized language plus the original
track name in parentheses when it is not just the ISO code), cast + remote device picker, SyncPlay (members, chat, reactions, floating
reaction overlay), skip segment (cooldown + auto-dismiss. After settings and segments load, native
chrome re-evaluates immediately so AutoSkip and the skip button do not wait on the next time tick.
TV D-pad Up from the transport bar focuses skip when it is offered. Down returns to Settings. Skip
stays in the Left/Right focus ring after Settings. When chrome is hidden, TV Up/Down reveal chrome
onto skip when it is offered. Enter still skips while the offer is on screen), next-episode countdown/autoplay, and
touch gestures (brightness/volume swipe with dim overlay, double-tap skip with ripple). Dedicated
TV remote Rewind / Fast-forward keys are intercepted in `MainActivity.DispatchKeyEvent` (they are
not D-pad events) and skip by the configured SkipBack / SkipForward durations even when chrome is
visible. Hold scrubs. When playback reaches the end, a series episode with a successor shows
the next-episode offer. A movie
or last episode closes the player. Closing native chrome on Android/iOS restores the hero:
Play when present (movie, serie, episode pages), otherwise the season episode card that
had focus, and Embla carousels keep the snap from before the WebView was hidden (otherwise
0-width reInit jumps the episode/season row to the last card). Icons use
a bundled Phosphor TTF (`Resources/Fonts/Phosphor.ttf`, registered as font family `"Phosphor"`).
codepoints are kept in `NativePlayerGlyphs` and must stay in sync with `Phosphor.cs`'s CSS class
names. Labels not covered by `IStringLocalizer<SharedResource>` use hard-coded FR/EN fallbacks in
`NativeStrings` (ASCII only). Windows intentionally has **no** XAML overlay - it stays full Blazor
+ Video.js, per the table above.

Player quality options: **Original (Np)** is remux / bitstream copy when the client cannot
Direct Play the file. The ladder also offers the same height as a bitrate-capped encode
(e.g. `1080p` next to `Original (1080p)`), then lower rungs (`720p`, `480p`, ...).

Direct Play (muxed file, no ffmpeg) is used on native Android/iOS/Mac when the device reports
the source container plus both codecs. Android TV can play `matroska` + HEVC + AAC that way.
The native overlay then switches audio and text subs with ExoPlayer / AVPlayer track overrides
(same settings UI as HLS). Image subs and a non-Original quality step still promote the
session to HLS. iOS/Mac do not advertise `matroska` (AVPlayer).

Web and Windows Video.js never get video Direct Play. A muxed file would lock the first
audio/sub: Video.js hands native playback to the browser, and Chromium does not expose
in-container `audioTracks` (Safari is the exception for MP4). Maintainers recommend HLS
or DASH instead:

- [video.js#6442](https://github.com/videojs/video.js/issues/6442) (MKV multi-audio works in ExoPlayer, not Video.js)
- [Audio Tracks](https://docs.videojs.com/tutorial-audio-tracks.html) (switch is not handled by Video.js, VHS/HLS only)

Those clients always take demuxed HLS (remux copy, or encode if the codec is not
HLS-compatible). Web and Windows advertise video codecs from `MediaSource.isTypeSupported`
on fMP4 strings (`hvc1...`), not `<video>.canPlayType` (progressive `hev1`). HEVC Main 10
is encoded to H.264: MSE often accepts 8-bit `hvc1` (so About lists hevc) then Video.js
rejects the real Main 10 tag. Demuxed `CODECS` is video-only (`hvc1` without `mp4a`) so
VHS does not call `isTypeSupported` on a combined type. HEVC `CODECS` uses general_level_idc
(`L120` for 4.0, not `L4`). Audio-only Direct Play is unchanged.

Direct Play audio formats must exist for the file container (`audio-matroska-aac` and the
other container/codec pairs next to each `video-*` format). Missing those forced HLS remux
even when the decoder could play the file. ffprobe names (`mpeg2video`, `pcm_s16le`,
`h265`, `dca`, `av01`, `av02`) are matched to those catalog ids. AV1 is listed for
MKV/MP4/WebM/MOV/M4V/MPEG-TS. AV2 is listed for MKV/MP4/WebM. Direct Play still
needs the device to report an `av2` decoder (rare).

Playback ladder: Direct Play when the device can open the file. Otherwise remux copy
runs one ffmpeg from the play/seek point to EOF. Encode keeps the configured
`EncoderThrottleBufferSegments` window. Direct Play audio/sub changes are reported
on playback-progress so admin/history show the tracks in use. History is one row per
play. Track language on that row (and watch stats) freezes when the session is first
marked completed.

## Windows: why not MediaElement

K7 HLS playlists are **fMP4** and emit `#EXT-X-MAP` (init segment). WinUI / Media Foundation **does not support** that HLS tag:

- [HTTP Live Streaming (HLS) tag support - Windows apps | Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/hls-tag-support) (`EXT-X-MAP` = Not Supported)

Until Microsoft adds support (or K7 offers a Windows-specific playlist without `#EXT-X-MAP`), Windows MAUI must decode via **Chromium MSE** (Video.js in WebView2). Because the surface is already Blazor/WebView2, Windows also keeps the Blazor control overlay - no separate XAML chrome. Codec capability reporting on Windows therefore reflects WebView2 **MSE** (same `deviceCapabilities.js` as web), not MediaElement - see `CodecService` under `src/Clients/MAUI/Platforms/Windows/`.

Shared branch flag: [`WindowsVideoPlayback.UsesWebVideoPlayer`](../../src/Clients/Shared/Helpers/WindowsVideoPlayback.cs).

`PlaybackOptionsDialog` lists movie releases by resolution, audio languages, codec, size, and
Local vs Federated when several files exist (not the media title).

## Demuxed HLS timestamps (Web vs Android)

K7 serves separate audio and video fMP4 playlists on one keyframe-aligned timeline (`#EXTINF`).
Web (Video.js / hls.js) follows playlist timing, so small `tfdt` drift is invisible.

Android ExoPlayer (Media3) uses fMP4 `tfdt`. Encode playlists keep
`#EXT-X-INDEPENDENT-SEGMENTS` (forced IDR at each cut). Remux omits that tag.

Many remux sources (Heroes HEVC Main 10) are **open GOP**: one IDR at t=0, then CRA
at every playlist keyframe. ffmpeg marks each CRA as a sync sample. ExoPlayer flushes
the decoder at that flag, so linear play cuts at every GOP even with a single ffmpeg
process and correct `tfdt` (ggg/hhh). Remux serve/finalize clears the first-sample
sync flag on intra-window CRA `.m4s`. The first file of each ffmpeg window keeps
sync: that bitstream has no prior refs, and demoting it froze ExoPlayer (iii).
IDR files stay sync. Android Original seek uses EXACT so PREVIOUS_SYNC does not
snap back to t=0. After a window fills Target, keep one BufferSize lookahead while
the last GET is still near the ready frontier (pause stops further remux).

A lazy ffmpeg window that resets timestamps to ~0, or an audio-copy
timeline that is not the same as video, looks like a discontinuity: audio drifts, then
snaps back.

Video copy windows use `-copyts -start_at_zero` so IDR cuts stay coherent. Audio copy keeps
source PTS (input+output `-ss`, `-copyts -copytb 1`, `-output_ts_offset`, no `-start_at_zero`).
Video remux seeks a short pad (~200ms) past the playlist IDR with `-noaccurate_seek` (not the
GOP midpoint: collapsed interior IDRs still exist in the bitstream and a midpoint land makes
`-segment_times` cut on the wrong frames - jumps / rewind). The HLS keyframe builder keeps any
source keyframe inside `RemuxSeekClearanceMs` (250ms) as a playlist boundary so that pad cannot
hit a hidden IDR. AAC encode subtracts encoder
priming from `-output_ts_offset`.

On serve, rebase video **copy** `tfdt` only for a true ffmpeg window reset (1s or more). Do
**not** flatten the source ~83ms video CTS/composition onto the playlist (that 20ms align
caused a constant A/V offset on remux). Persist that rebase on disk only after ffmpeg has
exited (include the kept after-pad). Writing while the muxer still holds the file mixed
absolute and window-relative `tfdt` (rewind + cuts). Video **encode** (720p and below) uses the 20ms
align and subtracts the first-sample CTS. Hardware-encoder delay (VAAPI / NVENC / AMF,
often hundreds of ms) sits under 1s and otherwise stays as late video that ExoPlayer
drops (lipsync + sporadic rewind). HLS encode forces `-bf 0`, disables scene-cut
(`-sc_threshold 0`, nvenc `-no-scenecut 1 -zerolatency 1`), and applies the ladder
`b:v` / `maxrate` / `bufsize` 5x (720p is 2.8 / 3.5 Mbps, not scale-only). Extra
scene-cut IDRs make `-f segment` cut off the shared keyframe timeline.

Encode cuts use exact keyframe `-ss` (accurate seek) on the **deliver** segment (no
remux pad). `-segment_times` and `force_key_frames source` follow source keyframes
(playlist grid). Hardware encoders also need `-g` capped and IDR forcing
(`-forced_idr` on AMF, `-forced-idr` on NVENC). Do not add output `-t` on encode
(`-copyts` + `-f segment` yields zero frames). Remux **seek** still uses a short past-IDR
`-ss` + `-noaccurate_seek` with one-segment pads. Sequential remux continue (previous
playlist index already ready) skips the before-pad but keeps that past-IDR seek: an
accurate remux `-ss` snaps to the previous IDR and writes the same GOP twice. Remux
`-segment_times` also cuts at the
exclusive window end so the last `.m4s` closes on its playlist boundary (otherwise
the next GOP is packed in and ExoPlayer jumps). That closer file is deleted after
ffmpeg exits. Do not extend input `-to` by a seek pad: remux lands on the keyframe,
not past mid-GOP. Do not micro-rebase **audio copy** onto `#EXTINF`.

- ffmpeg window padding must not punch holes in playlist indices. A missing `N.m4s` with
  later segments on disk restarts ffmpeg at `N` only when that process is not already running
- remux continue after a ready `N-1.m4s` must not rewrite `N-1` as a seek pad (that forced
  1-2 segment windows and video micro-freezes). It must still past-IDR seek: accurate remux
  `-ss` replays the previous GOP (rewind). Seek windows still pad
- remux copy runs one ffmpeg from the play/seek point to EOF. A seek does not kill
  that process when the requested segment is already on disk or still inside the
  running window (rewind into written .m4s, or jump ahead while ffmpeg is catching up).
  Restart only if the segment is missing and outside that window (seek before the
  remux start, or ffmpeg already idle)
- encode keeps `EncoderThrottleBufferSegments` (`requested + BufferSize` windows)
- when `HlsSegments` rows exist they drive copy and transcode (shared audio group / ABR).
  Without them, playback starts immediately on a 6s equal-length transcode grid
- new keyframe HLS rows collapse bursts from `RemuxSeekClearanceMs` (250ms) up to 1s
  (ExoPlayer). Keyframes closer than 250ms stay as boundaries so remux seek cannot land
  on a hidden IDR. Existing rows stay until HLS is recomputed
- sidecar WebVTT extract must not block `.vtt` HTTP. A cache miss returns 503 and ffmpeg
  fills the cache in the background. Do not return empty WEBVTT 200: ExoPlayer caches that
  and never shows cues. Waiting on extract (~10s) stalled A/V prefetch.

Android disables skip-silence and seeks with previous-sync so AAC gaps do not resync by
dropping audio. The native loading veil, seek bar, skip, and resume capture must use
ExoPlayer `CurrentPosition` (and `OnRenderedFirstFrame`), not toolkit
`MediaElement.Position`, which can stay 0 or freeze after a mid-stream resume. A skip or
seekbar tap on stale toolkit position then jumps minutes backward. `ExoPlaybackBridge`
publishes ExoPlayer duration and position into `IPlayerService` for the native overlay
when `MediaElement.Duration` stays 0 on demuxed HLS. Stale `.m4s` from an older rebase can be deleted
from the transcode cache. Streaming HLS rebinds the MediaElement ExoPlayer with long HTTP
timeouts and VTT load retries on HTTP 503 (`AndroidExoHlsTuning`). Web and Windows Video.js
wrap VHS xhr the same way (`ensureVtt503RetryXhr` in `videoplayer.js`): exponential backoff on
`.vtt` 503 so VHS does not disable the subtitle track on the first cold-cache miss.

## Subtitle appearance

`VideoPlayerSettingsDto` font / size / color / background opacity / shadow settings are mapped
by `SubtitleStyleHelper` (same values as the settings preview). Font size scales by
`DeviceType` (phone/watch, tablet, desktop, TV). Web and Windows Video.js apply them via
`applySubtitleStyle` (CSS on `.vjs-text-track-cue` / `::cue`). Android ExoPlayer applies
them via `AndroidSubtitleStyle` (`CaptionStyleCompat` on `PlayerView.SubtitleView`). Image-based
burn-in (PGS) cannot be restyled client-side.

After save or reset, the server pushes `ReceiveVideoPlayerSettingsUpdated` on the K7 hub
(user identity group). `VideoPlayerUxSettingsSync` applies the payload to `IPlayerService`
and subtitle CSS/ExoPlayer styling on every connected client (other browser tabs, phone, TV)
without restarting playback.

## Related

- Client hosts: [developing.md](developing.md#maui-blazor-hybrid)
- Architecture layers: [architecture.md](architecture.md)
