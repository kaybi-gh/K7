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

Player quality options: **Original (Np)** is remux / bitstream copy. The ladder also offers the
same height as a bitrate-capped encode (e.g. `1080p` next to `Original (1080p)`), then lower
rungs (`720p`, `480p`, ...).

## Windows: why not MediaElement

K7 HLS playlists are **fMP4** and emit `#EXT-X-MAP` (init segment). WinUI / Media Foundation **does not support** that HLS tag:

- [HTTP Live Streaming (HLS) tag support - Windows apps | Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/hls-tag-support) (`EXT-X-MAP` = Not Supported)

Until Microsoft adds support (or K7 offers a Windows-specific playlist without `#EXT-X-MAP`), Windows MAUI must decode via **Chromium MSE** (Video.js in WebView2). Because the surface is already Blazor/WebView2, Windows also keeps the Blazor control overlay - no separate XAML chrome. Codec capability reporting on Windows therefore reflects WebView2, not MediaElement - see `CodecService` under `src/Clients/MAUI/Platforms/Windows/`.

Shared branch flag: [`WindowsVideoPlayback.UsesWebVideoPlayer`](../../src/Clients/Shared/Helpers/WindowsVideoPlayback.cs).

`PlaybackOptionsDialog` lists movie releases by resolution, audio languages, codec, size, and
Local vs Federated when several files exist (not the media title).

## Demuxed HLS timestamps (Web vs Android)

K7 serves separate audio and video fMP4 playlists on one keyframe-aligned timeline (`#EXTINF`).
Web (Video.js / hls.js) follows playlist timing, so small `tfdt` drift is invisible.

Android ExoPlayer (Media3) uses fMP4 `tfdt` plus `#EXT-X-INDEPENDENT-SEGMENTS`. A lazy ffmpeg
window that resets timestamps to ~0, or an audio-copy timeline that is not the same as video,
looks like a discontinuity: audio drifts, then snaps back.

Video copy windows use `-copyts -start_at_zero` so IDR cuts stay coherent. Audio copy keeps
source PTS (input+output `-ss`, `-copyts -copytb 1`, `-output_ts_offset`, no `-start_at_zero`).
Video still seeks at GOP midpoint with `-noaccurate_seek`. AAC encode subtracts encoder
priming from `-output_ts_offset`.

On serve, rebase video **copy** `tfdt` only for a true ffmpeg window reset (1s or more). Do
**not** flatten the source ~83ms video CTS/composition onto the playlist (that 20ms align
caused a constant A/V offset on remux). Video **encode** (720p and below) uses the 20ms
align and subtracts the first-sample CTS: NVENC delay (~500-600ms) sits under 1s and
otherwise stays as late video that ExoPlayer drops (lipsync + sporadic rewind). HLS
encode forces `-bf 0`, disables scene-cut (`-sc_threshold 0`, nvenc `-no-scenecut 1
-zerolatency 1`), and applies the ladder `b:v` / `maxrate` / `bufsize` 5x (720p is
2.8 / 3.5 Mbps, not scale-only). Extra scene-cut IDRs make `-f segment` cut off the
shared keyframe timeline.

Encode cuts use exact keyframe `-ss` (accurate seek) on the **deliver** segment (no
remux pad). `-segment_times` and `force_key_frames source` follow source keyframes
(playlist grid). Hardware encoders also need `-g` capped and IDR forcing
(`-forced_idr` on AMF, `-forced-idr` on NVENC). Do not add output `-t` on encode
(`-copyts` + `-f segment` yields zero frames). Remux still uses midpoint `-ss`
+ `-noaccurate_seek` with one-segment pads. Remux `-segment_times` also cuts at the
exclusive window end so the last `.m4s` closes on its playlist boundary (otherwise
the next GOP is packed in and ExoPlayer jumps). That closer file is deleted after
ffmpeg exits. Do not extend input `-to` by the midpoint seek pad: remux lands on
the keyframe, not the midpoint. Do not micro-rebase **audio copy** onto `#EXTINF`.

- ffmpeg window padding must not punch holes in playlist indices. A missing `N.m4s` with
  later segments on disk restarts ffmpeg at `N` only when that process is not already running
- when `HlsSegments` rows exist they drive copy and transcode (shared audio group / ABR).
  Without them, playback starts immediately on a 6s equal-length transcode grid
- new keyframe HLS rows collapse bursts shorter than 1s (ExoPlayer). Existing rows stay
  until HLS is recomputed
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
timeouts and VTT load retries on HTTP 503 (`AndroidExoHlsTuning`).

## Related

- Client hosts: [developing.md](developing.md#maui-blazor-hybrid)
- Architecture layers: [architecture.md](architecture.md)
