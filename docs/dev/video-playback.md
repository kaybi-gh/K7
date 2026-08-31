# Video playback (clients)

How first-party clients play video, and why the MAUI hosts differ by platform.

## MAUI layout during play

| Platform | Decode surface | Controls |
|---|---|---|
| Android | Direct Play, HLS remux/encode, offline files: **ExoPlayer** (Media3 via MediaElement) | Text cues: ExoPlayer `SubtitleView` (UX style via `CaptionStyleCompat`). Chrome: native XAML `NativeVideoPlayerOverlay` (ZIndex 5). Amlogic TV: `SurfaceView` + Media3 tunneling on, audio offload off |
| iOS | MediaElement (AVPlayer) | Same native XAML chrome |
| Windows | Direct Play + offline: **LibVLC**. HLS transcode: **Video.js** (WebView2) | Native XAML chrome for both (LibVLC and HLS). HLS keeps WebView2 visible under the overlay for Video.js frames only. Remote-control sessions hide the overlay so Blazor `RemoteControlPanel` receives input |
| Web (WASM) | Video.js | Blazor `VideoPlayerControlsOverlay` |

Browse / library UI stays Blazor Hybrid. On Android/iOS, when `IPlayerService.IsVisible` is true, MAUI hides the BlazorWebView and shows the native decode surface + XAML chrome. **Windows** hides the WebView for LibVLC Direct Play / local files. HLS transcode keeps WebView2 visible under the same native XAML chrome (Video.js video element only, no Blazor HUD). Web WASM keeps Video.js + Blazor controls. Control plane remains [`IPlayerService`](../../src/Clients/Shared/Interfaces/IPlayerService.cs).

[`WindowsVideoPlayback`](../../src/Clients/Shared/Helpers/WindowsVideoPlayback.cs) routes by URL: `ShouldUseLibVlc` for muxed `/direct-stream` and `file://`. `ShouldUseWebVideoPlayer` for HLS (`manifest.m3u8`). Windows **audio** still uses WebView2 (`WindowsAudioPlayback.UsesWebAudioPlayer`).

Android video (muxed `/direct-stream`, HLS remux/encode, and offline `file://` downloads) uses
**CommunityToolkit MediaElement** backed by **ExoPlayer / Media3**. Auth is `Authorization` on the
MediaElement URI headers (and a shared `DefaultHttpDataSource` factory with long connect/read
timeouts for slow HLS init). Direct Play MKV resume uses HTTP Range seeks natively. Amlogic TV
boxes (e.g. Nokia Streaming Box 8000) use `AndroidViewType=SurfaceView`, `setKeepContentOnPlayerReset`,
and Media3 tunneling enabled (audio offload off) so HDMI present fences stay stable on Amlogic.
Dolby Vision / HDR10 keep their native MIME (`video/dolby-vision` or HEVC + PQ) so the TV can show
its HDR banner. Do not remap DV to plain HEVC on Amlogic. Overlay chrome does not refresh the seek bar
while hidden. The BlazorWebView is hidden (opacity 0) during native play but stays running so
SignalR can push live video-player settings.

Windows Direct Play still uses LibVLC. Chromecast still uses
`ephemeral_token` on the URL because the receiver is a remote device.

`/direct-stream` is a Range-capable file response. Before writing the body, Kestrel buffering and the min response data rate are disabled (`X-Accel-Buffering: no` for nginx). Otherwise a player can pause reading once its buffer is full, Kestrel aborts the socket, and the picture freezes.

Offline / local files (`file://` or a filesystem path from the download store) open via MediaElement
`FromUri(file://...)` on Android (Exo DefaultDataSource) and `FromFile` on iOS.
`StreamUriService` builds a `file://` URI for offline sessions (`new Uri(androidPath)` throws
`UriFormatException` because the path has no scheme).

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
`NativeStrings` (ASCII only). Windows uses the same XAML overlay as Android/iOS.

Player quality options: **Original (Np)** is remux / bitstream copy when the client cannot
Direct Play the file. The ladder also offers the same height as a bitrate-capped encode
(e.g. `1080p` next to `Original (1080p)`), then lower rungs (`720p`, `480p`, ...).

Direct Play (muxed file, no ffmpeg) is used on native Android/iOS/Mac/Windows when the device
reports the source container plus both codecs. Android TV plays `matroska` + HEVC + EAC3 via
ExoPlayer track selection. Windows MAUI uses LibVLC for the same formats. Android text cues use
ExoPlayer `SubtitleView` (`CaptionStyleCompat`). Windows Direct text cues use a sibling XAML
WebVTT layer on `RootGrid`. A non-Original quality step still promotes the session to HLS.
iOS/Mac do not advertise `matroska` (AVPlayer). Do not promote Direct Play to HLS burn-in just
to show PGS.

Web Video.js never gets video Direct Play. A muxed file would lock the first
audio/sub: Video.js hands native playback to the browser, and Chromium does not expose
in-container `audioTracks` (Safari is the exception for MP4). Maintainers recommend HLS
or DASH instead:

- [video.js#6442](https://github.com/videojs/video.js/issues/6442) (MKV multi-audio works in ExoPlayer, not Video.js)
- [Audio Tracks](https://docs.videojs.com/tutorial-audio-tracks.html) (switch is not handled by Video.js, VHS/HLS only)

The Web client always takes demuxed HLS (remux copy, or encode if the codec is not
HLS-compatible). GetStreamUri starts the video and audio ffmpeg jobs as soon as the
session is created so Video.js is not waiting on a cold `init.m4s` after its playlist
waterfall. Web advertises video codecs from `MediaSource.isTypeSupported`
on fMP4 strings (`hvc1...`), not `<video>.canPlayType` (progressive `hev1`). HEVC Main 10
is encoded to H.264: MSE often accepts 8-bit `hvc1` (so About lists hevc) then Video.js
rejects the real Main 10 tag. Demuxed `CODECS` is video-only (`hvc1` without `mp4a`) so
VHS does not call `isTypeSupported` on a combined type. HEVC `CODECS` uses general_level_idc
(`L120` for 4.0, not `L4`). Audio-only Direct Play is unchanged. Windows MAUI reports
LibVLC Direct Play formats (`LibVlcWindowsCapabilities`) instead of MSE.

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

## Windows: LibVLC Direct Play, Video.js HLS transcode

K7 HLS playlists are **fMP4** and emit `#EXT-X-MAP` (init segment). WinUI / Media Foundation **does not support** that HLS tag:

- [HTTP Live Streaming (HLS) tag support - Windows apps | Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/hls-tag-support) (`EXT-X-MAP` = Not Supported)

LibVLC demuxed HLS on Windows also failed reliably (adaptive never joined AUDIO, sample rate 0 on DDP). Windows MAUI therefore uses a **split pipeline**:

- **Direct Play** (muxed `/direct-stream`, offline `file://`): **LibVLC 4** + native XAML chrome (`WindowsVlcVideoPlayer`, loopback `VlcAuthProxy`). Real codecs (matroska, HEVC, EAC3) are preserved.
- **HLS** (encoded quality, burn-in, files that cannot Direct Play): **Video.js** in WebView2, same as Web WASM. The server never returns HLS **remux** for `OperatingSystem.Windows`. `GetStreamUri` always **transcodes** to h264/aac so MSE/VHS stays reliable (`VideoCodecsOnly=true` on the master).

LibVLC Direct Play uses D3D11 callbacks bound after `VideoView.Initialized` (LibVLC 4 dropped `--winrt-d3dcontext`). Direct Play seeks reopen with `:start-time` when HTTP `SetTime` is ignored. The loading veil stays until the first frame. Keyboard goes to the overlay via window `PreviewKeyDown`/`PreviewKeyUp`. Fullscreen uses `AppWindowPresenterKind.FullScreen`. The BlazorWebView is hidden only during LibVLC play. For Video.js HLS it stays visible under native XAML chrome (`InputTransparent`) so frames paint while the overlay owns input.

Pipeline swaps are exclusive. Direct to HLS fully disposes LibVLC (`StopWindowsVlc` + recreate
next Direct). HLS to Direct disposes Video.js (`DisposeWebPlayerAsync`). Text subtitles on
Windows HLS use a full sidecar VTT (`/subtitles/{index}.vtt` via the stream-fetch bridge +
Video.js `addRemoteTextTrack`), not VHS `EXT-X-MEDIA` segments (unreliable in WebView2).
Volume is shared via `IPlayerService.Volume`. On Windows Direct, LibVLC maps UI 0-1 to software
volume 0-200 so perceived loudness matches Video.js/WebView2 (HTML5 0-1). The WASAPI "K7"
session is left alone (mmdevice owns it). Do not also drive it from `VolumeService` or Direct
becomes quieter than HLS. App exit during Direct Play: `AppWindow.Closing` runs
`PrepareForAppExit` (clear D3D Present callbacks, soft-release WinUI-owned SwapChain wrappers
without Dispose, LibVLC `Stop`/`Dispose` on a background thread) to avoid
`ExecutionEngineException` from Present-after-teardown.

Codec capability reporting for Direct Play is [`LibVlcWindowsCapabilities`](../../src/Clients/Shared/Helpers/LibVlcWindowsCapabilities.cs) (matroska / HEVC / EAC3). HLS transcode targets Web MSE (h264/aac), not LibVLC caps.

`PlaybackOptionsDialog` lists movie releases by resolution, audio languages, codec, size, and
Local vs Federated when several files exist (not the media title). Play without the dialog
sends no track indexes so the server `TrackSelector` applies Settings -> Video playback
preferences. Confirming the dialog sends those indexes and they win over settings. Next
episode keeps the current audio/subtitle languages when those tracks exist on the next file.

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

Android video clock comes from ExoPlayer (`ExoPlaybackBridge` / `GetExoPlaybackPositionSeconds`
into `IPlayerService`), not toolkit `MediaElement.Position`. A skip or seekbar tap on a stale
toolkit position used to jump minutes backward. Stale `.m4s` from an older rebase can be
deleted from the transcode cache. Sidecar SRT uses `GET /api/indexed-files/{id}/subtitles/{index}.vtt`
(503 retry while Exo or the XAML loader waits). Web Video.js wraps VHS xhr the same way
(`ensureVtt503RetryXhr` in `videoplayer.js`): exponential backoff on `.vtt` 503 so VHS
does not disable the subtitle track on the first cold-cache miss. Windows Direct uses the
XAML sidecar loader. Windows HLS uses Video.js remote text tracks via the stream-fetch bridge.

## Subtitle appearance

`VideoPlayerSettingsDto` font / size / color / background opacity / shadow settings are mapped
by `SubtitleStyleHelper` (same values as the settings preview). Font size scales by
`DeviceType` (phone/watch, tablet, desktop, TV). Web and Windows HLS Video.js apply them via
`applySubtitleStyle` (CSS on `.vjs-text-track-cue` / `::cue`) using the same
`SubtitleStyleHelper.ToFontSizePx` values. Video.js is configured with `nativeTextTracks: false`
and `textTrackSettings: false` so cues stay on those absolute px sizes instead of Video.js
`1.4em` / `fontPercent` (or native `::cue` height-relative sizing). Android text subs use ExoPlayer
`SubtitleView` + `CaptionStyleCompat` (`AndroidExoSubtitleStyle`). Windows Direct text subs use
the XAML sidecar label (same helper). Image-based burn-in (PGS) cannot be restyled client-side.

After save or reset, the server pushes `ReceiveVideoPlayerSettingsUpdated` on the K7 hub
(user identity group). `VideoPlayerUxSettingsSync` applies the payload to `IPlayerService`
and subtitle CSS/ExoPlayer styling on every connected client (other browser tabs, phone, TV)
without restarting playback.

## Related

- Client hosts: [developing.md](developing.md#maui-blazor-hybrid)
- Architecture layers: [architecture.md](architecture.md)
