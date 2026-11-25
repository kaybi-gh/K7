#if WINDOWS || LINUX
using System.Globalization;
using K7.Clients.MAUI.Playback;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;

namespace K7.Clients.MAUI;

/// <summary>
/// Desktop (Windows / Linux) LibVLC Direct Play glue between <c>IPlayerService</c> and
/// <see cref="IDesktopVlcVideoPlayer"/>. HLS transcode stays on Video.js in the WebView on
/// both hosts. Platform partials provide the player factory, window integration and JS bridge.
/// </summary>
public partial class BlazorPage
{
    private IDesktopVlcVideoPlayer? _vlcPlayer;
    private bool _vlcEventsHooked;
    private string? _directTrackOverrideUrl;

    internal bool IsDesktopVlcActive => _vlcPlayer?.IsActive == true;

    internal bool IsDesktopWebVideoActive =>
        _playerService.Source is not null
        && WindowsVideoPlayback.ShouldUseWebVideoPlayer(
            _playerService.Source.MimeType,
            _playerService.Source.Url);

    private partial IDesktopVlcVideoPlayer CreateDesktopVlcPlayer();

    /// <summary>Right after <see cref="IDesktopVlcVideoPlayer.Play"/> (Windows resets the mixer gain).</summary>
    partial void OnDesktopVlcSessionStarted();

    /// <summary>After the player is disposed by <see cref="StopDesktopVlc"/>.</summary>
    partial void OnDesktopVlcSessionStopped();

    /// <summary>
    /// The native engine cannot run at all (Linux: libvlc missing or VLC 3). Platforms that
    /// implement it swap to the Video.js transcode ladder and tell the user. The default
    /// path reports a plain media failure.
    /// </summary>
    partial void OnDesktopVlcUnavailable(string detail, ref bool handled);

    internal const string LibVlcUnavailablePrefix = "libvlc-unavailable";

    private void InitializeDesktopVlcRequests()
    {
        _playerService.SwitchAudioTrackRequested += OnSwitchAudioTrack;
        _playerService.SwitchSubtitleTrackRequested += OnSwitchSubtitleTrack;
    }

    private void DisposeDesktopVlcPlayer()
    {
        try
        {
            _vlcPlayer?.Dispose();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc dispose on close " + ex.GetType().Name);
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
    }

    internal void SetDesktopVlcSurfaceVisible(bool visible) =>
        _vlcPlayer?.SetSurfaceVisible(visible);

    internal static bool ShouldUseDesktopVlc(PlayerSource? source) =>
        source is not null
        && WindowsVideoPlayback.ShouldUseLibVlc(source.MimeType, source.Url);

    internal bool TryOpenDesktopVlc(PlayerSource source)
    {
        if (!ShouldUseDesktopVlc(source) || string.IsNullOrEmpty(source.Url))
            return false;

        // The WebView compositor sits on top of the native surface (ZIndex does not win).
        // Dispose Video.js and hide the WebView before Play so Direct audio never starts
        // under a leftover HLS frame.
        TryEvaluateWebViewJs(
            "try{if(window.blankK7VideoSurfaces)blankK7VideoSurfaces();"
            + "if(window.K7&&K7.setNativePlayerActive)K7.setNativePlayerActive(true,false);}catch(e){}");
        HideBlazorWebViewForNativeVideo();
#if !LINUX
        // Windows keeps a hidden MediaElement. Linux has none.
        NativePlayer.IsVisible = false;
        try
        {
            NativePlayer.Stop();
            NativePlayer.Source = null;
        }
        catch
        {
        }
#endif

        _vlcPlayer ??= CreateDesktopVlcPlayer();
        HookDesktopVlcOnce();
        VlcSubtitleStyle.SetSettings(
            _playerService.VideoPlayerUxSettings ?? VlcSubtitleStyle.GetSettings());

        var startSeconds = source.PendingSeekTime is double pending && pending > 1
            ? pending
            : 0;
        // Text SRT/VTT: overlay owns paint - never pass a VLC :sub-track ordinal.
        var subtitleOrdinal = _playerService.SelectedSubtitleTrack is { IsTextBased: true }
            ? null
            : ResolveVlcSubtitleOrdinal();
        // Rate before Play: the Linux player applies it through a media option at start and
        // would otherwise reopen right after the first frames to honour a persisted rate.
        _vlcPlayer.SetRate(_playerService.PlaybackRate > 0 ? _playerService.PlaybackRate : 1);
        _vlcPlayer.Play(
            source.Url,
            ResolveNativePlayerAuthorizationHeader(),
            startSeconds,
            ResolveVlcAudioOrdinal(),
            subtitleOrdinal,
            hlsAudioTrackIndex: null,
            _playerService.Duration);
        if (_playerService.SelectedSubtitleTrack is { IsTextBased: true })
            _vlcPlayer.SetOverlayOwnsTextSubs(true);
        OnDesktopVlcSessionStarted();
        _vlcPlayer.SetVolume(_playerService.Volume);
        _vlcPlayer.SetMuted(_playerService.IsMuted);
        _vlcPlayer.SetRate(_playerService.PlaybackRate > 0 ? _playerService.PlaybackRate : 1);
        _vlcPlayer.ApplyAspect(_playerService.AspectRatio);

        var kind = LocalPlaybackUrl.IsLocalFile(source.Url)
            ? "file"
            : StreamingSourceKind.IsHls(source.MimeType, source.Url)
                ? "hls"
                : "direct";
        if (startSeconds > 1)
            _playerService.CurrentTime = startSeconds;

        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Buffering;
        VlcPlayerLog.Info(
            "bind kind="
            + kind
            + " pipeline=vlc url="
            + VlcPlayerLog.SummarizeUrl(source.Url)
            + " mime="
            + (source.MimeType ?? "-")
            + " quality="
            + (_playerService.SelectedQuality?.Label ?? "-")
            + " start="
            + startSeconds.ToString("F1", CultureInfo.InvariantCulture)
            + "s");
        return true;
    }

    /// <summary>
    /// Stops and fully disposes LibVLC so Direct and Video.js never run in parallel.
    /// Next Direct Play recreates a fresh player.
    /// </summary>
    internal void StopDesktopVlc()
    {
        var player = _vlcPlayer;
        if (player is null)
            return;

        // Drop the field first so overlay/control handlers cannot touch a half-disposed engine
        // during a fast Direct -> HLS quality swap.
        _vlcPlayer = null;
        _vlcEventsHooked = false;

        try
        {
            player.Stop();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc pipeline stop " + ex.GetType().Name);
        }

        try
        {
            player.Dispose();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc pipeline dispose " + ex.GetType().Name);
        }

        OnDesktopVlcSessionStopped();
    }

    internal bool TryHandleDesktopVlcPlay()
    {
        if (!IsDesktopVlcActive)
            return false;

        _vlcPlayer!.Resume();
        return true;
    }

    internal bool TryHandleDesktopVlcPause()
    {
        if (!IsDesktopVlcActive)
            return false;

        _vlcPlayer!.Pause();
        return true;
    }

    internal bool TryHandleDesktopVlcStop()
    {
        if (!IsDesktopVlcActive)
            return false;

        StopDesktopVlc();
        return true;
    }

    internal bool TryHandleDesktopVlcMute(bool muted)
    {
        if (!IsDesktopVlcActive)
            return false;

        _vlcPlayer!.SetMuted(muted);
        return true;
    }

    internal bool TryHandleDesktopVlcVolume(double volume)
    {
        if (!IsDesktopVlcActive)
            return false;

        // IPlayerService volume -> LibVLC software gain only (parity with Video.js element volume).
        _vlcPlayer!.SetVolume(volume);
        return true;
    }

    internal bool TryHandleDesktopVlcRate(double rate)
    {
        if (!IsDesktopVlcActive)
            return false;

        _vlcPlayer!.SetRate(rate);
        return true;
    }

    internal bool TryHandleDesktopVlcAspect(AspectRatioMode mode)
    {
        if (!IsDesktopVlcActive)
            return false;

        _vlcPlayer!.ApplyAspect(mode);
        return true;
    }

    internal void UpdateDesktopVlcAuthorization()
    {
        _vlcPlayer?.UpdateAuthorization(ResolveNativePlayerAuthorizationHeader());
    }

    internal void ApplyPendingDesktopSubtitleStyle()
    {
        if (IsDesktopVlcActive)
            _vlcPlayer?.RefreshSubtitleStyle();
    }

    internal void ReleaseSidecarTextSubtitles() =>
        _vlcPlayer?.SetOverlayOwnsTextSubs(false);

    internal void NotifySidecarTextSubtitles(bool ready)
    {
        if (!IsDesktopVlcActive)
            return;

        _vlcPlayer!.SetOverlayOwnsTextSubs(ready);
    }

    internal bool TryGetDesktopVlcMediaSeconds(out double seconds)
    {
        seconds = 0;
        if (!IsDesktopVlcActive)
            return false;

        seconds = _vlcPlayer!.PositionSeconds;
        return true;
    }

    private double GetDesktopVlcPositionSeconds() =>
        TryGetDesktopVlcMediaSeconds(out var seconds) ? seconds : 0;

    private Task SeekDesktopVideoAsync(double positionSeconds) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!IsDesktopVlcActive)
                return;

            var resumePlayback = _playerService.PlaybackState
                is Server.Domain.Enums.PlaybackState.Playing
                or Server.Domain.Enums.PlaybackState.Buffering;

            var targetSeconds = Math.Max(0, positionSeconds);
            // Prefer metadata duration: VLC Length can be 0/short around reopen.
            var knownDuration = Math.Max(_playerService.Duration, _vlcPlayer!.DurationSeconds);
            if (knownDuration > 1)
            {
                _vlcPlayer.PinDuration(knownDuration);
                targetSeconds = Math.Min(targetSeconds, knownDuration);
            }

            _vlcPlayer.Seek(targetSeconds);
            _playerService.CurrentTime = targetSeconds;
            if (knownDuration > 1 && _playerService.Duration <= 1)
                _playerService.Duration = knownDuration;
            if (resumePlayback)
                _vlcPlayer.Resume();
        });

    private void HookDesktopVlcOnce()
    {
        if (_vlcPlayer is null || _vlcEventsHooked)
            return;

        _vlcEventsHooked = true;
        _vlcPlayer.Playing += OnDesktopVlcPlaying;
        _vlcPlayer.Paused += OnDesktopVlcPaused;
        _vlcPlayer.Ended += OnDesktopVlcEnded;
        _vlcPlayer.EncounteredError += OnDesktopVlcError;
        _vlcPlayer.PositionChanged += OnDesktopVlcPosition;
        _vlcPlayer.DurationChanged += OnDesktopVlcDuration;
        _vlcPlayer.FirstFrame += OnDesktopVlcFirstFrame;
        _vlcPlayer.Reopening += OnDesktopVlcReopening;
    }

    private void OnDesktopVlcPlaying()
    {
        if (!IsDesktopVlcActive)
            return;

        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Playing;
        if (_playerService.Source is { PendingSeekTime: double pending } source
            && pending > 1
            && Math.Abs(_vlcPlayer!.PositionSeconds - pending) <= 30)
        {
            source.PendingSeekTime = null;
        }
    }

    private void OnDesktopVlcPaused()
    {
        if (!IsDesktopVlcActive)
            return;

        _playerService.PlaybackState = NativeVideoPlaybackEnd.PromoteIfMediaEnded(
            Server.Domain.Enums.PlaybackState.Paused,
            engineIsPlaying: false,
            isOpeningSource: false,
            isVisible: _playerService.IsVisible,
            durationSeconds: Math.Max(_playerService.Duration, _vlcPlayer?.DurationSeconds ?? 0),
            positionSeconds: _vlcPlayer?.PositionSeconds ?? _playerService.CurrentTime);
    }

    private void OnDesktopVlcEnded()
    {
        if (IsDesktopVlcActive)
            _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Ended;
    }

    private void OnDesktopVlcError(string detail)
    {
        if (!IsDesktopVlcActive)
            return;

        VlcPlayerLog.Warn("vlc playback failed " + VlcPlayerLog.SummarizeUrl(detail));
        if (detail.StartsWith(LibVlcUnavailablePrefix, StringComparison.Ordinal))
        {
            var handled = false;
            OnDesktopVlcUnavailable(detail, ref handled);
            if (handled)
                return;
        }

        if (detail.Contains("401", StringComparison.Ordinal)
            || detail.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            _ = TryRecoverNativeVideoAuthAsync("vlc " + detail);
            return;
        }

        ReportNativePlayerMediaFailedToServer("vlc " + detail);
    }

    private void OnDesktopVlcPosition(double seconds)
    {
        if (!IsDesktopVlcActive || seconds < 0)
            return;

        _playerService.CurrentTime = seconds;
    }

    private void OnDesktopVlcDuration(double seconds)
    {
        if (!IsDesktopVlcActive || seconds <= 0)
            return;

        // Once metadata duration is known, never replace it with VLC Length.
        var known = _playerService.Duration;
        if (known > 1)
        {
            if (Math.Abs(seconds - known) > 0.5 && seconds >= known * 0.9 && seconds <= known * 1.1)
                _playerService.Duration = Math.Max(known, seconds);
            return;
        }

        _playerService.Duration = seconds;
    }

    private void OnDesktopVlcReopening()
    {
        // Seek/audio reopen must not flash a zero duration on the seekbar.
        if (_playerService.Duration > 1)
            _vlcPlayer?.PinDuration(_playerService.Duration);
        _nativeOverlay?.ShowTransientVeil();
    }

    private void OnDesktopVlcFirstFrame()
    {
        _nativeOverlay?.NotifyFirstFrameReady();
        var url = _playerService.Source?.Url;
        if (_directTrackOverrideUrl == url)
            return;

        _directTrackOverrideUrl = url;
        try
        {
            _vlcPlayer?.LogEsTracks();
        }
        catch (InvalidOperationException)
        {
            // LibVLCSharp can expose null MediaTrack entries before ES are ready.
        }
    }

    private void OnSwitchAudioTrack(string trackName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (IsDesktopVlcActive)
                TrySwitchVlcAudioTrack(trackName, attempt: 0);
        });
    }

    private void OnSwitchSubtitleTrack(string? slug)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (IsDesktopVlcActive)
                TrySwitchVlcSubtitleTrack(slug, attempt: 0);
        });
    }

    private int? ResolveVlcAudioOrdinal()
    {
        if (_playerService.SelectedAudioTrack is not { } audio)
            return null;

        var ordered = _playerService.AudioTracks.OrderBy(t => t.Index).ToList();
        var index = ordered.FindIndex(t => t.Index == audio.Index);
        return index >= 0 ? index : null;
    }

    /// <summary>
    /// VLC :sub-track ordinal among image/PGS ES only. Text tracks are overlay-owned
    /// and must not inflate the ordinal into VLC's Text list.
    /// </summary>
    private int? ResolveVlcSubtitleOrdinal()
    {
        if (_playerService.SelectedSubtitleTrack is not { } sub || sub.IsTextBased)
            return null;

        var ordered = _playerService.SubtitleTracks
            .Where(t => !t.IsTextBased)
            .OrderBy(t => t.Index)
            .ToList();
        var index = ordered.FindIndex(t => t.Index == sub.Index);
        return index >= 0 ? index : null;
    }

    private void TrySwitchVlcAudioTrack(string trackName, int attempt)
    {
        if (!IsDesktopVlcActive)
            return;

        if (!trackName.StartsWith("audio-", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(trackName.AsSpan(6), out var fileStreamIndex))
        {
            VlcPlayerLog.Warn("vlc audio switch bad slug=" + trackName);
            return;
        }

        var ordered = _playerService.AudioTracks.OrderBy(t => t.Index).ToList();
        var index = ordered.FindIndex(t => t.Index == fileStreamIndex);
        if (index < 0)
        {
            VlcPlayerLog.Warn(
                "vlc audio switch missing stream="
                + fileStreamIndex.ToString(CultureInfo.InvariantCulture));
            if (attempt < 5)
                ScheduleVlcTrackRetry(() => TrySwitchVlcAudioTrack(trackName, attempt + 1));
            return;
        }

        var catalog = ordered[index];
        if (_vlcPlayer!.TrySelectAudio(index, catalog.Language, catalog.Name))
            return;

        if (attempt < 5)
            ScheduleVlcTrackRetry(() => TrySwitchVlcAudioTrack(trackName, attempt + 1));
    }

    private void TrySwitchVlcSubtitleTrack(string? slug, int attempt)
    {
        if (!IsDesktopVlcActive)
            return;

        if (slug is null)
        {
            _vlcPlayer!.TrySelectSubtitle(null, null, null);
            return;
        }

        // Text SRT/VTT: XAML sidecar owns paint + live style. Do not select VLC SPU.
        if (_playerService.SelectedSubtitleTrack is { IsTextBased: true })
        {
            _vlcPlayer!.TrySelectSubtitle(null, null, null);
            _vlcPlayer.SetOverlayOwnsTextSubs(true);
            return;
        }

        if (!slug.StartsWith("sub-", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(slug.AsSpan(4), out var fileStreamIndex))
        {
            VlcPlayerLog.Warn("vlc sub switch bad slug=" + slug);
            return;
        }

        var imageTracks = _playerService.SubtitleTracks
            .Where(t => !t.IsTextBased)
            .OrderBy(t => t.Index)
            .ToList();
        var index = imageTracks.FindIndex(t => t.Index == fileStreamIndex);
        if (index < 0)
        {
            VlcPlayerLog.Warn(
                "vlc sub switch missing stream="
                + fileStreamIndex.ToString(CultureInfo.InvariantCulture));
            if (attempt < 5)
                ScheduleVlcTrackRetry(() => TrySwitchVlcSubtitleTrack(slug, attempt + 1));
            return;
        }

        var catalog = imageTracks[index];
        if (_vlcPlayer!.TrySelectSubtitle(index, catalog.Language, catalog.Name))
            return;

        if (attempt < 5)
            ScheduleVlcTrackRetry(() => TrySwitchVlcSubtitleTrack(slug, attempt + 1));
    }

    private static void ScheduleVlcTrackRetry(Action retry)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            MainThread.BeginInvokeOnMainThread(retry);
        });
    }
}
#endif
