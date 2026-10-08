# Video playback (clients)

How first-party clients play video, and why the MAUI hosts differ by platform.

## MAUI layout during play

| Platform | Decode surface | Controls |
|---|---|---|
| Android | Direct Play, HLS remux/encode, offline files: **ExoPlayer** (Media3 via MediaElement) | Text cues: ExoPlayer `SubtitleView` (UX style via `CaptionStyleCompat`). Chrome: native XAML `NativeVideoPlayerOverlay` (ZIndex 5). All Android: `SurfaceView` + Media3 tunneling **off**. Android TV: audio offload **on**, Dolby Vision Profile 8 defaults to HEVC/HDR10 |
| iOS | MediaElement (AVPlayer) | Same native XAML chrome |
| Windows | Direct Play + offline: **LibVLC**. HLS transcode: **Video.js** (WebView2) | Native XAML chrome for both (LibVLC and HLS). HLS keeps WebView2 visible under the overlay for Video.js frames only. Remote-control sessions hide the overlay so Blazor `RemoteControlPanel` receives input |
| Linux (GTK4, experimental) | Direct Play + offline: **LibVLC 4** (vmem frames into a `Gtk.Picture`). HLS transcode: **Video.js** (WebKitGTK) | Native XAML chrome for both, like Windows: the WebKitGTK view paints Video.js under the overlay and loses pointer targeting (`SetCanTarget(false)`). Music: audioplayer.js through a loopback auth proxy |
| Web (WASM) | Video.js | Blazor `VideoPlayerControlsOverlay` |

Browse / library UI stays Blazor Hybrid. On Android/iOS, when `IPlayerService.IsVisible` is true, MAUI hides the BlazorWebView and shows the native decode surface + XAML chrome. **Windows and Linux** hide the WebView for LibVLC Direct Play / local files. HLS transcode keeps the WebView visible under the same native chrome (Video.js video element only, no Blazor HUD). Web WASM keeps Video.js + Blazor controls. Control plane remains [`IPlayerService`](../../src/Clients/Shared/Interfaces/IPlayerService.cs).

[`WindowsVideoPlayback`](../../src/Clients/Shared/Helpers/WindowsVideoPlayback.cs) routes by URL: `ShouldUseLibVlc` for muxed `/direct-stream` and `file://`. `ShouldUseWebVideoPlayer` for HLS (`manifest.m3u8`). Windows **audio** still uses WebView2 (`WindowsAudioPlayback.UsesWebAudioPlayer`).

Android video (muxed `/direct-stream`, HLS remux/encode, and offline `file://` downloads) uses
**ExoPlayer / Media3** on a CommunityToolkit `PlayerView` surface. The toolkit `MediaManager` is
not an Exo `IPlayerListener` on Android TV (a TV Exo host must not tick the MAUI UI thread). HTTP streams
are `SetMediaSource` on the tuned player. `MediaElement.Source` is skipped so the toolkit cannot
open a second pipeline. Error recovery, the seek bar buffer, and logs read ExoPlayer
`CurrentPosition` / `BufferedPosition` / `PlayerError` because `MediaElement.Position` stays at 0
without a MediaManager listener. Auth is `Authorization` on a shared `DefaultHttpDataSource` factory with
long connect/read timeouts for slow HLS init. Direct Play MKV resume uses HTTP Range seeks natively.
Android uses `AndroidViewType=SurfaceView` and `setKeepContentOnPlayerReset`. PlayerView
artwork and the idle play-in-circle bitmap (`exo_edit_mode_logo`) stay off: close/stop
keeps a black shutter instead of scaling that placeholder to the panel. When video **hides**
(close or switch to audio) the MediaElement is parked / `GONE` **before** `Stop`. Do not
set `MediaElement.Source = null` on Android: that posts a late `PlaybackException` and
PlayerView paints "media could not be loaded" plus the edit-mode logo. Keep-content is
dropped so the last frame cannot linger over the Blazor shell. The next open must restore
PlayerView and VideoSurfaceView to `VISIBLE` and reattach `PlayerView.Player` **before**
`SetMediaSource` / `Prepare`. `PlayerView.setPlayer` is a no-op when the instance is already
set, so a surface destroyed on close is never given back unless the player is nulled then
set again. Do that only before Prepare. Doing it after Prepare (or calling
`clearVideoSurface` on stop) leaves audio with no video output and the startup veil waits
forever. Delayed placeholder `GONE` posts after close are ignored once video is visible
again, so a quick HLS reopen cannot hide the new surface. `exo_error_message` and
`exo_artwork` are forced `GONE`. HDMI tunneling stays **off** on every device, including Amlogic TV boxes
(Nokia Streaming Box 8000). Tunneling plus EAC3 Direct Play can throw ExoPlayer
`ERROR_CODE_FAILED_RUNTIME_CHECK` (1004) at t=0 depending on HDMI sink and firmware, so two
identical boxes can disagree. NVIDIA Shield already needed tunneling off (Media3 hitch on Tegra).
Android TV ExoPlayer uses decoder fallback, FFmpeg extension
renderers ON, default MediaCodec order (no vendor-first reorder), audio offload ON.
That player uses Media3
`DefaultLoadControl` **Default** unless the user picks Large / Extra large (Media3 stock
LoadControl). Extra-large is 100s min / 120s max. Phone/tablet stay on Exo defaults unless the
user picks a size. The choice is device-local (`VideoExoBuffer`: auto / default /
large / extralarge) under Settings -> Video playback. HDMI auto frame rate is a
device-local setting (`VideoHdmiAfr`: disabled / device / tv). **Disabled** leaves the TV at its current Hz (often 59.94).
**Scale on device** keeps the current panel size and switches rate only.
**Scale on TV** picks the 1x/2x/2.5x HDMI size closest to the file (1080p film
goes to `1080p @ 23.98` instead of 4K). Amlogic (Nokia Streaming Box) defaults
to **disabled**: 24 Hz HDMI on that HAL can hitch more than 23.976 on 59.94
(Direct Play with AFR off was smoother). Other Android TV defaults to
scale on device. When AFR is on, `preferredDisplayModeId` matches content fps from
the file (ffprobe `avg_frame_rate` on the stream session) before ExoPlayer starts, then waits until the HDMI
mode is current. After the
switch, Amlogic HAL AFR is set to policy=0 so the
vendor HAL cannot retime HDMI. Policy 2 and the previous HDMI mode are restored after
`Stop` (wait until the saved mode is current). A later play with AFR off also restores a
leftover 24 Hz switch. Closing does not `Release` the tuned Exo instance - it `Stop`s and
`ClearMediaItems` so the next title does not keep a decoder clocked to the old rate. Do not pin
policy=0 when app AFR is disabled (AFR-off leaves the HAL default). Files scanned before `FrameRate` was stored get a one-shot
ffprobe on the first `CreateStreamSession` (value is persisted on the video tracks). A full
library rescan is not required. 23.976 prefers 24 / 47.95, then 59.94. Direct Play MKV
often has no Exo fps, so the server value is required. The HUD lists supported HDMI modes
(current marked `*`) and cadence (1x / 2x / 2.5x / 3:2 pulldown). ExoPlayer
`Surface.setFrameRate` is **off** when HDMI AFR is disabled. The panel stays at 59.94.
Exo poking the surface fights Amlogic HAL AFR. When AFR is on, Media3 OnlyIfSeamless stays. Audio offload is **on** for Android TV. HDMI tunneling stays off. Offload bypasses the
Sonic time-stretch processor, so playback speed != 1x would be a no-op on Direct Play
(offloaded original track). `TrySetAndroidVideoSpeed` therefore disables offload while
speeding (via `AndroidExoHlsTuning.SetAudioOffloadForSpeed`, re-decoding to a PCM + Sonic
path) and restores the policy default at 1x. HLS already decodes to PCM, so speed always
worked there. On Android TV, chrome-hidden native
overlay is taken out of composition (`View.GONE` plus off-screen translation) so a
full-screen transparent MAUI Grid cannot blend over the decode surface every vsync (Amlogic
hitch). Amlogic HEVC then drops ~3 frames every 10s the first time chrome hides during
playback. This is **not** composition, surface size, hardware-plane promotion, tunneling,
AFR, or the 10s progress report timer (all ruled out empirically - a TextureView, which is
never promoted to a hardware plane, drops identically). The trigger is a decode/render
timing de-sync. The cure is a **one-time native layout pass of an extra view in the video's
parent tree**. Opening the playback settings panel does exactly that (its native
`ContentViewGroup` goes `0x0` -> `560x872` and permanently stops the drops, even after it
closes). The fix replicates that automatically: the first time chrome hides,
[`NativePlaybackSettingsPanel.PrewarmNativeLayout`](../../src/Clients/MAUI/Controls/Video/NativePlaybackSettingsPanel.cs)
lays the panel out once off-screen at `Opacity=0` (no flash, no interactive open, no chrome
change), then hides it - once per session, on all Android TV as a safety net (harmless where
no drops occur - the bug is Amlogic-specific)
([`MaybeRunTvDecodeResync`](../../src/Clients/MAUI/Controls/Video/NativeVideoPlayerOverlay.cs)).
All devices keep `SurfaceView` (optimal AV sync / power). The prewarm alone clears the drops,
so no TextureView fallback is needed. Keep `PlayerView` non-focusable (software DPAD rings). Text cues use a
software `SubtitleView` layer so they do not GPU-blend over the HDMI overlay. Admins can turn on **Playback stats**
in the native overlay playback menu: a corner HUD (sibling of chrome, so TV can still
undraw the overlay - stats on must not keep the full-screen Grid in composition) mirrors the admin dashboard stream decision (Direct / Transmux /
Transcode, source -> stream codecs, burn-in vs sidecar, encoder, reason) plus live
device stats (HDMI Hz vs content fps, dropped frames, buffer, `host exo`, `buf default`,
AFR / DV / tunneling). The toggle is
`Capability.CanAccessAdmin` only and is stored on the device
(`VideoPlaybackNerdStats`). Android TV Dolby Vision Profile 8 defaults to **HEVC / HDR10**
(`VideoDvDecode`: empty = hevc on TV, native on phones). Native keeps `video/dolby-vision`
(TV DV banner). HEVC answers MediaCodec with `video/hevc` so the HAL plays the HDR10
base layer. Restart playback after changing it. If playback dies at start (ExoPlayer 1004 /
decoder init), native chrome stays usable and walks **Direct Play -> remux HLS (Original)**.
Cold remux `init.m4s` 503 looks like Video.js error 4 ("format not supported"). Stay on
remux for 25s instead of jumping to 1080p encode. Do not reload the HLS source (that
flips play/pause). Encode ladder only after that remux window. Logged as
`NativePlayer.QualityFallback`. If the ladder is exhausted
the player closes (`NativePlayer.PlaybackAborted`) and K7Snackbar shows MediaPlaybackUnplayable
after the Blazor WebView is restored. Closing the player force-hides chrome and resets overlay composition so the
Blazor UI is not left covered. Overlay chrome does not refresh the seek bar
while hidden. The BlazorWebView is hidden (opacity 0) during native play but stays running so
SignalR can push live video-player settings.

Windows Direct Play still uses LibVLC. Chromecast still uses
`ephemeral_token` on the URL because the receiver is a remote device.

`/direct-stream` is a Range-capable file response. Before writing the body, Kestrel buffering and the min response data rate are disabled (`X-Accel-Buffering: no` for nginx). Otherwise a player can pause reading once its buffer is full, Kestrel aborts the socket, and the picture freezes.

Offline / local files (`file://` or a filesystem path from the download store) open via MediaElement
`FromUri(file://...)` on Android (Exo DefaultDataSource) and `FromFile` on iOS.
`StreamUriService` builds a `file://` URI for offline sessions (`new Uri(androidPath)` throws
`UriFormatException` because the path has no scheme).

Seek buffering shows the overlay spinner only (last frame stays, no black veil). Android
lifts it on the next decoded frame. Windows Video.js lifts it when playback resumes.

`NativeVideoPlayerOverlay` (`src/Clients/MAUI/Controls/Video/`) targets 1:1 parity with the Blazor
`VideoPlayerControlsOverlay`: transport, seek bar with chapter ticks/sprite thumbnail preview and
hovered chapter title, playback settings (audio/subtitles/quality/speed/aspect, plus an
admin-only Playback stats toggle that shows a live HUD), with TV D-pad
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
Web does not transcode just because the source is taller than the screen. The browser
decodes and scales. Devices report both logical screen size (`DisplayScreenHeight`,
CSS pixels or DIP) and physical pixels (`DisplayResolutionHeight`, CSS * DPR or DIP *
density). When an encode is already required (unsupported codec, burn-in, missing HLS
segments), GetStreamUri caps output to the largest ladder rung that fits
`DisplayResolutionHeight` so a 4K file on a 1080p client is not re-encoded at 4K.

Direct Play (muxed file, no ffmpeg) is used on native Android/iOS/Mac/Windows when the device
reports the source container plus both codecs. Android also sends extra `vprofile:` tokens
(HEVC/AV1 Main vs Main 10, converted MediaCodec level, decoder max width x height) on
`SupportedMediaFormatIds`. When those tokens are present, GetStreamUri refuses Direct Play
(and HLS copy) for Main 10 / Dolby Vision / over-level / over-size even if a `video-*-hevc`
catalog id exists. Clients that send no tokens (Web, iOS, Windows) keep MIME-only matching.
Android TV plays `matroska` + HEVC + EAC3 via ExoPlayer track selection. Settings -> Video
playback on a native device can turn **audio passthrough** off. That stays on the device
(`VideoAudioPassthrough`) and is sent on the stream session (`AudioPassthrough=false`).
GetStreamUri then treats AC3/EAC3/DTS/TrueHD as not Direct Playable for that play
(remux/transcode to AAC) without rewriting the device capability list.

HLS AAC encode channel count: the Web client stores `achannels:N`
(`AudioContext.destination.maxChannelCount`) next to its format ids
(`AudioOutputChannelTokens`). `GetStreamUri` resolves `HlsAudioChannelPolicy` = min(source,
device output, encoder) restricted to 1/2/6/8, puts it on `StreamDecisionDto.StreamAudioChannels`
and passes `MaxAudioChannels` to the master. The master advertises `CHANNELS` as the delivered
count and adds `TranscodingAudioChannels=N` to the audio URIs, which the audio job (`-ac N`,
distinct `audio-aacNch-aX` cache dir) honours. A stereo browser gets 2ch AAC from an AC3 5.1
source; a 5.1 output keeps 6. No token (natives, old clients) keeps the source layout.
A stereo downmix of a surround source adds an explicit `pan=stereo|...` matrix
(`FfmpegStereoDownmix`, keyed by the ffprobe channel layout: voices forward, LFE kept)
next to `-ac 2`. libswresample's default mix buries dialogue and drops LFE. Unknown layouts
or a failed probe keep plain `-ac 2`. Windows MAUI uses LibVLC
for the same formats. Android text cues use
ExoPlayer `SubtitleView` (`CaptionStyleCompat`). Windows Direct text cues use a sibling XAML
WebVTT layer on `RootGrid`. A non-Original quality step still promotes the session to HLS.
iOS/Mac do not advertise `matroska` (AVPlayer). Do not promote Direct Play to HLS burn-in just
to show PGS.

Web Video.js: when remuxed fMP4 audio from MPEG-TS fails in MSE (`audio append`
from Video.js), the client reloads the same HLS master with `AudioTrackTranscodings={index}:aac`
on the manifest URL (same query param as `GetHlsStreamManifest` already supports). Video stays
remux copy. One retry per play.

Web Video.js never gets video Direct Play. A muxed file would lock the first
audio/sub: Video.js hands native playback to the browser, and Chromium does not expose
in-container `audioTracks` (Safari is the exception for MP4). Maintainers recommend HLS
or DASH instead:

- [video.js#6442](https://github.com/videojs/video.js/issues/6442) (MKV multi-audio works in ExoPlayer, not Video.js)
- [Audio Tracks](https://docs.videojs.com/tutorial-audio-tracks.html) (switch is not handled by Video.js, VHS/HLS only)

The Web client always takes demuxed HLS (remux copy, or encode if the codec is not
HLS-compatible). A 4K HEVC file on a 1080p monitor remuxes when MSE accepts the
codec. Encode (when required) is capped to the display ladder rung. GetStreamUri starts the video and audio ffmpeg jobs as soon as the
session is created so Video.js is not waiting on a cold `init.m4s` after its playlist
waterfall. Web advertises video codecs from `MediaSource.isTypeSupported`
on fMP4 strings (`hvc1...`), not `<video>.canPlayType` (progressive `hev1`). HEVC Main
vs Main 10 are separate `vprofile:hevc:main` / `vprofile:hevc:main10` tokens: an 8-bit
`hvc1` probe must not unlock Main 10 remux. Settings -> About lists those profiles
(and AC3/EAC3 when MSE reports `ac-3` / `ec-3`), not a flat `hevc` flag. When Main 10
MSE passes, HLS copies HEVC.
Otherwise GetStreamUri encodes to H.264. Demuxed `CODECS` is video-only (`hvc1` without
`mp4a`) so VHS does not call `isTypeSupported` on a combined type. HEVC `CODECS` uses
general_level_idc (`L120` for 4.0, not `L4`). AC3/EAC3 use the same MSE probe
(`ac-3` / `ec-3`). If the browser reports them, HLS remuxes audio, else AAC.
Native HLS (Android Exo) still encodes AC3/EAC3: copy often never finishes
`init.m4s`. DTS/TrueHD still encode to AAC. The AAC encode keeps the source channel
layout (no server-side `-ac` downmix): the master advertises the source `CHANNELS`, so
announcing N while delivering a forced stereo stream is what muted some 5.1 layouts.
ExoPlayer (or any client) downmixes N channels to the device output. Video.js
`MEDIA_ERR_DECODE` / `MEDIA_ERR_SRC_NOT_SUPPORTED` on Original quality steps the
encode ladder. Audio-only Direct Play is unchanged. Windows MAUI reports
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
- **HLS** (encoded quality, burn-in, files that cannot Direct Play): **Video.js** in WebView2, same as Web WASM. The server never returns HLS **remux** for **native** Windows (`ClientType.Native` + `OperatingSystem.Windows`). `GetStreamUri` always **transcodes** to h264/aac so MSE/VHS stays reliable (`VideoCodecsOnly=true` on the master). A web browser on Windows remuxes like any other web client.
- The native settings panel (Windows and Android TV) sizes to the longest audio or subtitle label, up to the free width above the transport bar. Audio flags use the language inferred from the track title when the container tag is `und`.

LibVLC Direct Play uses D3D11 callbacks bound after `VideoView.Initialized` (LibVLC 4 dropped `--winrt-d3dcontext`). Direct Play seeks reopen with `:start-time` when HTTP `SetTime` is ignored. The loading veil stays until the first frame. Keyboard goes to the overlay via window `PreviewKeyDown`/`PreviewKeyUp`. Fullscreen uses `AppWindowPresenterKind.FullScreen`. The BlazorWebView is hidden only during LibVLC play. For Video.js HLS it stays visible under native XAML chrome (`InputTransparent`) so frames paint while the overlay owns input.

Pipeline swaps are exclusive. Direct to HLS fully disposes LibVLC (`StopWindowsVlc` + recreate
next Direct). HLS to Direct disposes Video.js (`DisposeWebPlayerAsync`). Text subtitles on
Windows HLS use a full sidecar VTT (`/subtitles/{index}.vtt` via the stream-fetch bridge,
parsed into in-memory `VTTCue`s - no `track.src` / blob XHR), not VHS `EXT-X-MEDIA` segments
(unreliable in WebView2).
Volume is shared via `IPlayerService.Volume`. On Windows Direct, LibVLC maps UI 0-1 to software
volume 0-200 so perceived loudness matches Video.js/WebView2 (HTML5 0-1). The WASAPI "K7"
session is left alone (mmdevice owns it). Do not also drive it from `VolumeService` or Direct
becomes quieter than HLS. App exit during Direct Play: `AppWindow.Closing` runs
`PrepareForAppExit` (clear D3D Present callbacks, soft-release WinUI-owned SwapChain wrappers
without Dispose, LibVLC `Stop`/`Dispose` on a background thread) to avoid
`ExecutionEngineException` from Present-after-teardown.

Codec capability reporting for Direct Play is [`LibVlcWindowsCapabilities`](../../src/Clients/Shared/Helpers/LibVlcWindowsCapabilities.cs) (matroska / HEVC / EAC3). HLS transcode targets Web MSE (h264/aac), not LibVLC caps.

## Windows: optional MPC-HC / MPC-BE

Settings -> Video playback -> Advanced (this device) can send Play to **MPC-HC or MPC-BE** instead of LibVLC / Video.js. K7 does not embed madVR, LAV, or kaz. Those stay in MPC.

Flow:

1. `PlayerService.PlayIndexedFileAsync` intercepts when the device preset is on, and SyncPlay / remote receive / remote control / Chromecast are not active (`IExternalPlayerPolicy.SuppressExternalPlayer`, SyncPlay group, `IsControlling`, casting). Remote play onto this PC always uses the built-in player. Attach while MPC is already active is refused.
2. A stream session is created (progress reports still use it).
3. If Settings has a local folder for that library, the Windows client maps `IndexedFile.Path` against `Library.RootPath` and launches MPC with that filesystem path when the file exists. No extra session fields. No ephemeral token. Otherwise K7 falls back to `/api/indexed-files/{id}/direct-stream?ephemeral_token=...`. Extra args default `/fullscreen /close`, plus `/webport {port}` and `/start {ms}` from the K7 bookmark.
4. K7 does not show the in-app overlay. If `/start` is ignored, the host seeks once via `command.html` after the web UI answers.
5. K7 polls `variables.html` every 5s (localhost, default `127.0.0.1:13579`) and pumps `IPlayerService.ApplyExternalClock` so `PlaybackProgressTracker` keeps continue-watching. An HTTP token is revoked when MPC exits. Local-file play still reports through the same session.
6. `/webport` is applied at MPC process start only. An already-running instance without the web UI will not pick it up. If the web UI stays down, playback still starts. K7 cannot save a new position. Resume **into** MPC still uses the last K7 bookmark.

Launch failure shows a snackbar. There is no silent fallback to HLS / LibVLC for that Play. HTTP Direct Play is the fallback when the library path is not mapped.

`PlaybackOptionsDialog` lists movie releases by resolution, audio languages, codec, size, and
Local vs Federated when several files exist (not the media title). Play without the dialog
sends no track indexes so the server `TrackSelector` applies Settings -> Video playback
preferences. Confirming the dialog sends those indexes and they win over settings. Next
episode keeps the current audio/subtitle languages when those tracks exist on the next file.

## Linux: LibVLC Direct Play, Video.js HLS transcode

Same split as Windows, driven by the shared desktop glue
[`BlazorPage.DesktopVlc.cs`](../../src/Clients/MAUI/BlazorPage.DesktopVlc.cs) over
[`IDesktopVlcVideoPlayer`](../../src/Clients/MAUI/Playback/IDesktopVlcVideoPlayer.cs)
(Windows: `WindowsVlcVideoPlayer` + D3D11, Linux: `LinuxVlcVideoPlayer`).

- **Direct Play** (muxed `/direct-stream`, offline `file://`):
  [`LinuxVlcVideoPlayer`](../../src/Clients/MAUI/Platforms/Linux/LinuxVlcVideoPlayer.cs) uses
  LibVLC 4 **vmem callbacks** (`BGRA`, two buffers, alpha forced opaque): each displayed frame is copied into a
  `GLib.Bytes`, turned into a `Gdk.MemoryTexture` on the GTK main loop and set on the
  `Gtk.Picture` of [`LinuxVlcVideoView`](../../src/Clients/MAUI/Platforms/Linux/LinuxVlcVideoView.cs)
  (handler on the labs `GtkViewHandler`). Aspect modes map to the picture content fit, so VLC
  renders at the decoded size. libvlc itself comes from the `libvlc/linux-x64` bundle next to
  the app when present (release builds, `LinuxLibVlcBundle` locates it or `K7_LIBVLC_DIR`): the
  player dlopens the private libraries (ffmpeg and friends) and `libvlccore.so.9` by full path
  before libvlc so every plugin dependency resolves to the bundled sonames even next to a distro
  VLC 3, exports `VLC_PLUGIN_PATH` and runs with `--no-plugins-cache` (read-only install, no
  `plugins.dat`), without a bundle the resolver falls back to the system `libvlc.so.12`. The
  bundle contents and how it is built are described in
  [releasing.md](releasing.md#linux-libvlc-bundle). Auth goes through the same loopback
  `VlcAuthProxy`, seek and audio / PGS track switches reopen with `:start-time` like Windows. Text subtitles are the XAML
  sidecar label (no WinUI popup). Keyboard reaches the native overlay through a capture-phase
  `Gtk.EventControllerKey` on the window, fullscreen is `Gtk.Window.Fullscreen`.
  The CPU copy (decoded size x 4 bytes per frame) is fine for 1080p, a `Gtk.GLArea` /
  `libvlc_video_set_output_callbacks` OpenGL path is the follow-up for 4K.
- **Native chrome over GTK** (`BlazorPage.Linux`, `LinuxWidgetStack`, `LinuxGestureBridge`):
  GTK4 paints siblings in child order and the labs layout panel never reorders them (its
  `MapZIndex` even inverts `InsertAfter(parent, null)`, which inserts first, not last), so the
  page pins the stack explicitly when a Direct Play session starts: decode `Gtk.Picture` sent
  to the back, `NativeVideoPlayerOverlay` brought to the front. The labs `BlazorWebViewHandler`
  is not a `GtkViewHandler`, so `IsVisible` / `Opacity` / `InputTransparent` never reach the
  WebKit widget: the page calls `SetVisible(false)` / `SetCanTarget(false)` on it for the
  session and restores it when the session stops (HLS keeps the WebView). The labs handlers
  also never attach MAUI `GestureRecognizers`, `LinuxGestureBridge` wires `Gtk.GestureClick`,
  `EventControllerMotion` and `GestureDrag` on every overlay view that owns Tap / Pointer /
  Pan recognizers and raises the MAUI events through the internal `Send*` entry points
  (press count 1 = single tap, 2 = double tap). `LinuxVlcVideoPlayer` keeps one vmem buffer
  set per libvlc video output (keyed by the vmem `opaque` handle, freed in the cleanup
  callback): VLC 4 stops asynchronously, so a seek reopen used to free the buffers the previous
  decoder was still writing to. `gtk stack ...` log lines dump the RootGrid children
  (visibility, opacity, hit-testing, allocation) at session start and three seconds later.
  Four more labs gaps are closed on the K7 side: `LinuxRootLayoutDriver` marks the page root
  panel `IsExternallyManaged` and re-runs the MAUI measure / arrange pass from a frame-clock
  tick whenever the panel allocation changes (maximize, fullscreen) or the tree invalidates its
  measure (a panel toggling `IsVisible`), then re-sorts every layout panel by MAUI ZIndex (labs
  arranges the root once with the window default size and keeps insertion order).
  `LinuxLayoutHandler` keeps `InputTransparent` layouts with `CascadeInputTransparent=false`
  targetable (GTK picking skips a non-targetable widget and its whole subtree, so the chrome
  buttons never got clicks) while the gesture bridge forwards clicks on their empty area to the
  tappable sibling underneath (the gesture catchers), `LinuxBoxViewHandler` paints `BoxView`
  gradients / solid backgrounds with Cairo instead of the labs opaque grey placeholder (the
  chrome scrim), `LinuxCrashLog` writes unhandled exceptions with their stack to stderr and
  `~/.local/state/k7/crash.log`. `LinuxScrollViewHandler` reports the content height (labs
  returns 50px, which collapsed the settings and cast panels) and `LinuxOverlayTheme` installs
  a display-wide GTK CSS provider scoped to the `k7-video-overlay` class that flattens the
  themed GTK buttons, labels and scrollbars under the overlay (no borders, shadows or 38px
  minimums, white text, translucent hover). Clicks and pointer moves that land on the empty
  area of a pass-through layout are re-dispatched by picking inside the topmost sibling below
  (settings / cast rows get synthetic Tapped and hover, `Gtk.Button`s are activated). No vmem
  cleanup callback is registered: LibVLCSharp's trampoline declares it `ref IntPtr opaque`
  while libvlc passes `void *opaque` (the MediaPlayer GCHandle), so the marshaller dereferences
  the handle and the process aborts on every video output teardown (seek reopen, track switch,
  close). The vmem opaque is the buffer-set id (LibVLCSharp virtualizes it), stale sets are
  released on the next format callback and on Stop. Window-level and per-view pointer motion
  ignores GTK's synthesized motion events (same coordinates after a widget-tree change), which
  otherwise re-show the chrome right after every hide. The `LibVLC` instance is process-wide
  and so is the `MediaPlayer` (`_sharedLibVlc` / `_sharedPlayer`): VLC 4 recycles the video
  output across inputs but destroys it on player release, which aborts the `vlc-vout` thread
  on the nightly builds, so sessions only Stop and reuse the player (static vmem callbacks
  and events forward to the current session). libvlc runs with `--codec=avcodec`: VLC 4 removed `--spdif` on Linux and tries its
  S/PDIF pass-through decoder first for AC3 / E-AC3 / DTS, which the Pulse output rejects
  only after a ~10s timeout (clock stalled at every open and seek) before falling back to PCM. `LinuxUiScale` scales the chrome
  glyphs and the subtitle font like WinUI / Android would (GTK reports scale 1 under WSLg
  whatever the monitor, `K7_GTK_UI_SCALE` overrides the monitor-based factor). The label
  provider also re-emits colour / font / background in its own CSS block, because the labs
  per-widget block is dropped as a whole when one value is rejected. HLS on Linux renders the
  Blazor `<video>` element under the native overlay (same split as Windows HLS, not the Web HUD).
  Music: WebKitGTK's GStreamer backend fails on `blob:` audio, so `windowsStreamFetch.js` asks
  the bridge (`GetLocalStreamUrlAsync`) for a loopback `VlcAuthProxy` URL when the page carries
  `window.K7_LINUX_GTK` (user script injected by `BlazorPage.Linux`). Admin charts: the labs
  WebView handler does not strip query strings, so `ApexChartAssets.Prepare` points the charts
  at the unversioned Blazor-ApexCharts module on the GTK host, and the GTK host marker script
  strips the query of `app://` URLs in `URL.prototype.toString` (the path Blazor's `import`
  interop takes). The Linux device registers `achannels:2` so HLS transcodes are stereo
  (WebKitGTK stalls on multichannel AAC), Direct Play keeps the original tracks. F9 in the
  player dumps the GTK stacks and the sidecar label state to the terminal, `vlc clock ...`
  lines trace the raw libvlc clock every 5 seconds. The build also rewrites the
  Blazor-ApexCharts modules copied to the Linux output (`K7StripModuleVersionQuery`) so their
  static `import "./apexcharts.esm.js?ver=..."` loses the query the labs handler cannot serve.
  `Program.ConfigureWebKitSandbox` disables WebKit's bubblewrap sandbox under WSLg (or with
  `K7_WEBKIT_DISABLE_SANDBOX=1`): inside it the web process cannot reach the WSLg PulseAudio
  socket and every HTML5 media element fails ("PulseAudio: Unable to connect"), elsewhere the
  audio server directories are added to the sandbox. `LinuxSessionEnvironment` also exports
  `ALSOFT_DRIVERS=pulse,alsa` (OpenAL Soft, WebKit's Web Audio backend, otherwise probes
  PipeWire and bare ALSA) and `GST_PLUGIN_FEATURE_RANK=pulsesink:MAX`. The loopback proxy
  answers with CORS headers (`<audio crossorigin>`) and handles `OPTIONS` preflights, music
  proxies trace their requests (`vlc-proxy GET /direct range=...`). The Linux player maps
  libvlc Time by continuity (relative-to-start or absolute, whichever keeps the published clock
  continuous) because VLC 4 switches between both after `:start-time`, the seek spinner over
  Video.js hides once the clock advanced 0.75s (no Buffering state on WebKitGTK), and the
  volume popover only hides when GTK reports the pointer outside button and popover. Under
  WSLg (no usable GPU) or with `K7_WEBKIT_SOFTWARE_RENDERING=1`, `Program` exports
  `WEBKIT_DISABLE_DMABUF_RENDERER=1` (compositing itself stays on, MSE video needs it): the
  DMA-BUF renderer otherwise leaves the view black once an accelerated `<video>` goes away, the
  page also unmaps / maps the WebView whenever it becomes interactive again so WebKit redraws.
  `K7_GTK_TRACE_POINTER=1` logs the GTK enter / leave the gesture bridge relays. GTK skips the
  whole subtree of a non-targetable widget while picking, so a pass-through layout cannot be
  made hit-transparent natively: besides taps and hover, the bridge also drives
  `IGraphicsView` interactions (press, drag, release, hover) of the GraphicsViews that sit
  under the empty area of such a layout (volume slider). Pass-through hover follows MAUI
  semantics: entering a child view does not exit its pointer-aware ancestors, and
  `IsPointerOver` is true for a view whenever the pointer is in one of its descendants. Every
  loopback proxy session traces its first twelve requests (`vlc-proxy GET /direct range=...`).
  The labs `GtkWebViewManager` renders Blazor on `Dispatcher.CreateDefault()`, a thread that is
  not the GTK main thread: component handlers of `IPlayerService` events raised by the host
  must go through `InvokeAsync` (WinUI and Android share the UI thread with the renderer, so
  a direct `StateHasChanged` only fails on GTK). Linux Direct Play opens media with
  `:demux=avformat`: VLC 4's Matroska demuxer loads the Cues over HTTP but fails
  `DEMUX_SET_TIME`, so the core reads and discards from the first cluster up to `:start-time`
  (minutes-long resume, remaining duration reported as Length). `K7_VLC_DEMUX=native` restores
  the built-in demuxers. Playback rate
  changes reopen the input at the current position with a `:rate=` option: VLC 4 stores the
  rate in the player but the running HTTP input does not apply it live. `LinuxLabelTextShadow`
  maps the label `Shadow` to a CSS `text-shadow` (offset plus a thin outline) instead of the labs
  `box-shadow`, so the sidecar subtitle style stays readable.
- **HLS transcode**: Video.js in WebKitGTK **under the native chrome**, as on Windows.
  `ShowBlazorWebViewUnderNativeChrome` keeps the GTK WebView visible and calls
  `SetCanTarget(false)` on it (labs ignores `InputTransparent`), the overlay sits above it in
  the RootGrid ZIndex order and owns keyboard and pointer. Stream
  requests (manifests, segments, sidecar VTT, music) go through the same
  [`WindowsStreamFetchJsBridge`](../../src/Clients/MAUI/Playback/StreamFetch/WindowsStreamFetchJsBridge.cs)
  as Windows (HttpClient with the bearer): the `app://localhost` WebView origin is not in the
  server CORS list. `BlazorPage.Linux` and `Program` also register the `app` scheme as local,
  CORS-enabled and secure in WebKit, otherwise stylesheets and dynamic `import()` of ES modules (ApexCharts) fail with
  "Importing a module script failed".
- **Server**: native Linux is a normal native client for Direct Play (`AllowsVideoDirectPlay`)
  and a Video.js consumer for HLS (`UsesVideoJsHlsManifest`, video-only `CODECS`).
  `ForcesWindowsHlsEncode` stays Windows-only: remux copy is allowed when WebKitGTK MSE accepts
  the codec. The Linux `CodecService` advertises the LibVLC catalog
  (`LibVlcWindowsCapabilities`, VA-API or avcodec), so MKV / HEVC / EAC3 stay muxed.
- **Music**: audioplayer.js in WebKitGTK (`WindowsAudioPlayback.UsesWebAudioPlayer`).
- **Runtime**: LibVLCSharp 4 needs a **VLC 4** libvlc (`libvlc.so.12`, `libvlc.so.5` or the
  unversioned `-dev` symlink are probed). Distros ship VLC 3, install a VLC 4 nightly
  (Ubuntu: `ppa:videolan/master-daily`, or a nightlies.videolan.org build). Without a VLC 4
  libvlc the open fails with `libvlc-unavailable`, `BlazorPage.Linux` shows the
  `LinuxLibVlcUnavailable` warning snackbar once and promotes the session to the Video.js
  transcode ladder (`IPlayerService.TryRecoverPlaybackStartAsync`), so video still plays.
- **Logs**: on Linux `VlcPlayerLog` writes `K7 VLC info|warn ...` to stderr (terminal or
  `journalctl --user`), including native libvlc errors and warnings (`vlc-native ...`), the
  vmem format, play / reopen / seek reasons and the first-frame fallback.

## Demuxed HLS timestamps (Web vs Android)

K7 serves separate audio and video fMP4 playlists on one keyframe-aligned timeline (`#EXTINF`).
Web (Video.js / hls.js) follows playlist timing, so small `tfdt` drift is invisible.

Android ExoPlayer (Media3) uses fMP4 `tfdt`. Encode playlists keep
`#EXT-X-INDEPENDENT-SEGMENTS` (forced IDR at each cut). Remux omits that tag.

Many remux sources (Heroes HEVC Main 10) are **open GOP**: one IDR at t=0, then CRA
at every playlist keyframe. ffmpeg marks each CRA as a sync sample. ExoPlayer flushes
the decoder at that flag, so linear play cuts at every GOP even with a single ffmpeg
process and correct `tfdt`. Remux keeps sync RAP flags on disk so Video.js / MSE can
seek into the shared cache. Android Exo demotes CRA sync **in memory on serve only**
(never rewrite shared `.m4s`). Head-start RAP segments keep sync. IDR files stay sync.
Android Original remux seek uses EXACT so PREVIOUS_SYNC does not snap video back
to t=0 (or the last RAP) while independent AAC audio seeks. That froze the last
frame with a moving seek bar. Encode HLS keeps PREVIOUS_SYNC (forced IDR /
`#EXT-X-INDEPENDENT-SEGMENTS`). A remux seek jump also marks the landing index as
RAP so an already-ready `.m4s` is served with its CRA sync flag (no new head).

Remux copy uses **multi-head** ffmpeg: ready `N.m4s` files are immutable
(staging `head-{id}/` then atomic promote, never overwrite). A staging file is promoted
only once it is **closed**: `N+1.m4s` exists in the same staging dir, or the head's ffmpeg
exited. Complete fMP4 boxes are not enough: `frag_keyframe` flushes a moof+mdat at every
collapsed interior keyframe, so a mid-segment snapshot validates but misses the rest of
the GOP. Copying it froze truncated segments into the shared cache (video holes, Video.js
buffered ranges split at every such segment, Firefox seek never completing on resume).
Web resume: media playlists carry `#EXT-X-START:TIME-OFFSET=<raw resume>` and VHS seeks
there itself on first `play()` (`setupFirstPlay`). Do not snap it to the segment boundary
and do not seek again from JS when the URL has `startSeconds`: three seeks in a row on
Firefox MSE left the player in `seeking` forever. Video.js runs with `preload: 'none'`:
with any other value VHS starts the main segment loader at t=0 on `loadedmetadata`,
before `setupFirstPlay` seeks, so a resume first requested segment 0 (30s server wait on
the paired video segment the landing head never writes). `changeSourceAndSeek` calls
`play()` once right after `src()` so the master loads.
A seek/resume that lands
on a ready segment is served as-is. If a live head already covers the request (or the
nearest tip is within ~60s), wait on that head. Otherwise spawn a new head at the
landing index. First successful head owns shared `init.m4s`. Remux mux options use
`use_editlist=0` for stable init across heads. Absolute playlist `tfdt` still comes
from serve/finalize rebase (`absolute_tfdt` is not available on current ffmpeg builds).
Encode ladder stays windowed single-process for now.

After a window fills Target, keep one BufferSize lookahead while
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
(`-sc_threshold 0`, nvenc `-no-scenecut 1`), and applies the ladder
`b:v` / `maxrate` / `bufsize` 5x (720p is 2.8 / 3.5 Mbps, not scale-only). Extra
scene-cut IDRs make `-f segment` cut off the shared keyframe timeline.

Encode cuts use exact keyframe `-ss` (accurate seek) on the **deliver** segment (no
remux pad). `-segment_times` and `force_key_frames source` follow source keyframes
(playlist grid). Hardware encoders also need `-g` capped and IDR forcing
(`-forced_idr` on AMF, `-forced-idr` on NVENC). PGS burn-in `filter_complex` strips
source keyframe flags: use absolute `force_key_frames` (source PTS, because
`-copyts` keeps the encoder clock there) plus `-g 72`. Relative times are already
past after a mid-file `-ss` and never fire, so `-g 72` cuts every 3s. Serve rebase
then stamps playlist starts onto those oversized files and the next segment
overlaps (rollback). `-segment_times` stays relative to the keyframe because
`-start_at_zero` zeros the muxer only. Do not add output `-t` on encode
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
- federated master burn-in follows `SubtitleBurnInStreamIndex` on the manifest query.
  PGS is not a sidecar VTT. A playback URL that omits the index turns burn-in off.
  The video playlist then carries the index so the encode overlays that stream
- federated master audio follows `DefaultAudioTrackIndex` from the manifest query.
  When both the query and the decision omit the index, the paired audio kick
  does not start. Mapping stream 0 (often the video) as audio makes ffmpeg exit
  immediately, and a wiped-cache reset then restarts the empty remux head on every GET
- a stopped remux head that still has `head-*` staging is not a wiped cache
- local remux copy keeps cooperative heads to EOF. Piece-cache remux (federated
  `EnsureInputCoverage`) stays on a buffer window: Target is pinned to that head
  and must not inherit an EOF advertise, or ffmpeg reads sparse holes (video
  segments twice as long as audio, audio gaps, rollback). Seek never purges ready
  shared remux `.m4s`. Missing far targets spawn another head. Near targets wait
  on an existing tip (~60s). Ready remux segments stay immutable across clients
- encode seek that keeps the cache must delete `.m4s` from the new anchor forward.
  Leaving those files next to a re-anchored window overlaps PTS (rollback) even
  when each file's first sample matches audio. Segments before the anchor stay
  for seek-back
- a remux head started because `init.m4s` is missing must keep running until shared
  init is promoted. Stopping just because `From` and `From+1` are already cached
  (resume mid-movie, cache kept) spawned heads 77+ that died before ffmpeg wrote
  init, then the client 90s-timed-out `init.m4s` (HTTP 503, Video.js error 4)
- promoting remux init must look at the landing `.m4s` file itself, not
  `IsSegmentReadyOnDisk(0)`. Segment 0 readiness also requires init, so a From=0
  head that promoted and deleted staging `0.m4s` never copied init into the shared
  cache
- deleting the transcode cache under a live job must reset that job. An empty output
  plus a stale EOF `TargetSegmentIndex` used to start `init.m4s` near the end (70s wait,
  then Video.js error 4 / 1080p fallback). Recover stops zombie ffmpeg, forgets landings,
  and starts init at 0. But remux copy advertises Target = EOF and writes to `head-*`
  staging before promoting, and an encode resume window has no shared `.m4s` until the
  first one lands: both are cold starts, not wipes (`TranscodeWipedOutputPolicy`,
  `HasObservedReadyOutput`). Treating them as wipes killed the live head on the next
  request and the browser waited forever on `init.m4s`
- resume passes `StartSeconds` on session create; prefetch `EnsureSegment`s the landing
  index first, then `init.m4s`. Starting the AAC encode at 0 while Video.js asked for
  segment ~1055 stalled the audio playlist for tens of seconds. The prefetch is never
  awaited by the session create (it may stop/restart an AAC window or wait for a slot);
  the keyframe rows are read on the request DbContext before the task starts
- a job never counts itself in `WaitForTranscodeSlotAsync`: a remux job spawning a second
  head while its first head runs to EOF waited on itself, and `/api/stream-sessions` hung
- player closed (`Idle`) or media finished (`Ended`) in `UpdatePlaybackProgress` calls
  `ReleaseSessionAsync`: the session is detached from its jobs and ffmpeg is stopped on
  jobs left without any session (cache kept). SyncPlay / co-watching viewers each have
  their own stream session on the shared job, so the last one leaving stops it. Without
  this, a relaunch paid the stop of the still-running AAC window (~10s) on its first request
- after ffmpeg exits, `FinalizeClosedDeliverSegments` skips indices with no file. The
  finalize retry (50 x 20ms, for a file still flushing) cost ~1s per missing index over the
  whole window of an early-stopped head or AAC window (minutes), so stop / release /
  restart appeared to hang on "ffmpeg did not exit". Stops and slot waits are bounded
  (10s / 15s) with a warning as a safety net
- `MaxConcurrentTranscodes` 0 means unlimited (admin hint); the save command must not clamp
  it to 1, which serialized every ffmpeg on the server
- `MinDistanceSecondsToLiveHead` ignores heads whose `From` is after the request: a head
  never writes behind its start, so restart-from-0 while a resume head runs must spawn
- Web `hideVideoJs` hides only its player (never `blankK7VideoSurfaces`, which dispose()s
  every instance); `showVideoJs` undoes it on init / new source / play. Sidecar VTT is not
  awaited from Blazor and is injected only while the player tech is live (dispose bumps
  the token). Chrome stays visible while Buffering so the Play button is reachable when
  Firefox blocked autoplay (GetStreamUri + HLS load drop the click gesture). No implicit
  `Play()` on overlay tap: a play() while VHS is still joining restarts the seek. Natives
  blank surfaces themselves (`blankK7VideoSurfaces` from BlazorPage), not via `hideVideoJs`

- encode keeps `EncoderThrottleBufferSegments` (`requested + BufferSize` windows)
- encode seek no longer purges ready `.m4s`. A real seek (more than 3 minutes past the
  ready tip) re-anchors the window (`TranscodeJob.WindowStartIndex`) and keeps existing
  segments, so a seek back into an already-encoded range serves instantly instead of
  re-encoding. A closer forward request, including a quality switch, extends the same
  window. Re-anchoring that lookahead made the playhead segment 404 and the player jump
  ahead. `GetCurrentSegmentIndex`
  reports the contiguous ready run from `WindowStartIndex` (not the lowest index on disk), so
  far-away kept segments cannot fool the scan. Remux jobs leave `WindowStartIndex` at -1 and
  keep the disk scan
- when `HlsSegments` rows exist they drive copy and transcode (shared audio group / ABR).
  Without them, playback starts immediately on a 6s equal-length transcode grid
- new keyframe HLS rows collapse bursts from `RemuxSeekClearanceMs` (250ms) up to 1s
  (ExoPlayer). Keyframes closer than 250ms stay as boundaries so remux seek cannot land
  on a hidden IDR. Open-GOP CRAs that are followed (in decode order) by packets with
  lower PTS are also excluded: ffmpeg `-f segment` drops those trailing B-frames and
  leaves multi-frame holes in remux playlists. Existing rows stay until HLS is recomputed

- HLS media `init.m4s` waits up to 90s and `N.m4s` up to 180s before 503. That is not an
  early 503. Sidecar WebVTT extract must not block `.vtt` HTTP. A cache miss returns 503
  immediately and ffmpeg fills the cache in the background. Do not return empty WEBVTT 200:
  ExoPlayer caches that and never shows cues. Waiting on extract (~10s) stalled A/V prefetch.
- Web Video.js remembers the selected subtitle slug and re-applies it on `seeked` (and after
  plain `seek()`). VHS often disables EXT-X-MEDIA text tracks after a seek discontinuity
  even when A/V remux continues on the same master.
- Video.js / VHS has **no native retry** for HLS `EXT-X-MEDIA` subtitle errors: the playlist
  loader `error` handler disables the track immediately ("Disabling subtitle track"). Web
  and Windows Video.js therefore load text subs via the full sidecar
  `GET /api/indexed-files/{id}/subtitles/{index}.vtt` (`loadSidecarSubtitleTrack`), parse cues,
  and inject them on a remote text track **without** `src` (`manualCleanup` so quality/encode
  `src` swaps do not auto-drop the track). Pending sidecar is re-applied on `loadedmetadata`.
  A remote file id is not a local path. That request is proxied to the origin
  federation session `subtitles/{index}.vtt`, which extracts from the real file.
  A `blob:` `src` fails when the media element uses credentials (`ProgressEvent` status 0).
  Segmented HLS subtitle playlists are not used. Android Exo uses HLS VTT segments but
  never selects a text rendition until the sidecar VTT is ready: playback starts with text
  disabled (`TryDisableAndroidTextTrack` right after `Prepare`, so a forced/AUTOSELECT
  rendition cannot auto-load and 503-stall A/V), then `WarmAndroidHlsSubtitleThenSelectAsync`
  polls `GET /subtitles/{index}.vtt` (503 retry) and applies the Exo text override once the
  server returns 200. Audio and video are never gated on ffmpeg subtitle extraction, and the
  selected rendition never hits a 503 (which previously surfaced as an ExoPlayer
  "media could not be loaded" error and its placeholder art on the panel).

Android video clock comes from ExoPlayer (`ExoPlaybackBridge` / `GetExoPlaybackPositionSeconds`
into `IPlayerService`), not toolkit `MediaElement.Position`. A skip or seekbar tap on a stale
toolkit position used to jump minutes backward. Stale `.m4s` from an older rebase can be
deleted from the transcode cache. Sidecar SRT uses `GET /api/indexed-files/{id}/subtitles/{index}.vtt`
(503 retry while Exo or the XAML loader waits). Web Video.js wraps VHS xhr the same way
(`ensureVtt503RetryXhr` in `videoplayer.js`): exponential backoff on `.vtt` 503 so VHS
does not disable the subtitle track on the first cold-cache miss. Retries install at the
native `XMLHttpRequest` layer (so VHS cannot bypass module wrappers) and also wrap
`videojs.xhr` while keeping `Vhs.xhr.original = true`. Windows Direct uses the
XAML sidecar loader. Windows HLS uses Video.js remote text tracks via the stream-fetch bridge.

## Subtitle appearance

`VideoPlayerSettingsDto` font / size / color / background opacity / shadow settings are mapped
by `SubtitleStyleHelper` (same values as the settings preview). Font size scales by
`DeviceType` (phone/watch, tablet, desktop, TV). Values are density-independent (CSS px on
Web, MAUI `FontSize`, Android `COMPLEX_UNIT_SP`) so a phone Medium cue is 28sp, not 28
physical pixels. Web and Windows HLS Video.js apply them via
`applySubtitleStyle` (CSS on `.vjs-text-track-cue` / `::cue`) using the same
`SubtitleStyleHelper.ToFontSizePx` values. Video.js is configured with `nativeTextTracks: false`
and `textTrackSettings: false` so cues stay on those sizes instead of Video.js
`1.4em` / `fontPercent` (or native `::cue` height-relative sizing). Android text subs use ExoPlayer
`SubtitleView` + `CaptionStyleCompat` (`AndroidExoSubtitleStyle`, `SetFixedTextSize` in SP).
Windows Direct text subs use the XAML sidecar label (same helper). Image-based burn-in (PGS)
cannot be restyled client-side.

After save or reset, the server pushes `ReceiveVideoPlayerSettingsUpdated` on the K7 hub
(user identity group). `VideoPlayerUxSettingsSync` applies the payload to `IPlayerService`
and subtitle CSS/ExoPlayer styling on every connected client (other browser tabs, phone, TV)
without restarting playback.

## Related

- Client hosts: [developing.md](developing.md#maui-blazor-hybrid)
- Architecture layers: [architecture.md](architecture.md)
