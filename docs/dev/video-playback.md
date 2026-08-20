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
focus navigation; audio and subtitle labels are the normalized language plus the original
track name in parentheses when it is not just the ISO code), cast + remote device picker, SyncPlay (members, chat, reactions, floating
reaction overlay), skip segment (cooldown + auto-dismiss. TV D-pad can focus the skip intro/outro button while
chrome is visible), next-episode countdown/autoplay, and
touch gestures (brightness/volume swipe with dim overlay, double-tap skip with ripple). Dedicated
TV remote Rewind / Fast-forward keys are intercepted in `MainActivity.DispatchKeyEvent` (they are
not D-pad events) and skip by the configured SkipBack / SkipForward durations even when chrome is
visible. Hold scrubs. When playback reaches the end, a series episode with a successor shows
the next-episode offer. A movie
or last episode closes the player. Closing native chrome on Android/iOS restores the hero:
Play when present (movie, serie, episode pages), otherwise the season episode card that
had focus, and Embla carousels keep the snap from before the WebView was hidden (otherwise
0-width reInit jumps the episode/season row to the last card). Icons use
a bundled Phosphor TTF (`Resources/Fonts/Phosphor.ttf`, registered as font family `"Phosphor"`);
codepoints are kept in `NativePlayerGlyphs` and must stay in sync with `Phosphor.cs`'s CSS class
names. Labels not covered by `IStringLocalizer<SharedResource>` use hard-coded FR/EN fallbacks in
`NativeStrings` (ASCII only). Windows intentionally has **no** XAML overlay - it stays full Blazor
+ Video.js, per the table above.

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

The server keeps A/V in the source relationship, and only remaps `tfdt` when ffmpeg
has clearly restarted a window (`tfdt` off by 1s or more from the playlist start):

- audio bitstream-copy keeps source PTS (`-copyts`, `-output_ts_offset`, no `-start_at_zero`)
- video lazy windows still use `-start_at_zero`. Those fragments are rebased on serve
- keyframe HLS segments are at least 1s (collapsed at index / `ComputeHlsSegments` time)

Do not micro-rebase each track onto `#EXTINF` independently: tens of ms of AAC vs
keyframe composition error would become a constant lip-sync offset on Web and Android.

Android also disables skip-silence and seeks with previous-sync so AAC gaps do not resync by
dropping audio. Existing files keep their stored `HlsSegments` until HLS is recomputed.
Cached `.m4s` that were rewritten by an older tight rebase must be deleted (transcode cache)
or they keep the wrong `tfdt`.

## Related

- Client hosts: [developing.md](developing.md#maui-blazor-hybrid)
- Architecture layers: [architecture.md](architecture.md)
