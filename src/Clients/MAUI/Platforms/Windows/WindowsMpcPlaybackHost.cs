using System.Diagnostics;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;
using K7.Shared.QueryBuilders;
using Microsoft.Extensions.Localization;

namespace K7.Clients.MAUI.Platforms.Windows;

public sealed class WindowsMpcPlaybackHost(
    IStreamUriService streamUriService,
    IStreamingService streamingService,
    IK7ServerService k7ServerService,
    IDeviceStorageService deviceStorage,
    IK7Snackbar snackbar,
    IStringLocalizer<SharedResource> localizer) : IWindowsMpcPlaybackHost, IDisposable
{
    private static readonly TimeSpan WebUiWait = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly MpcWebClient _web = new();
    private readonly object _gate = new();
    private Process? _process;
    private CancellationTokenSource? _monitorCts;
    private Guid? _streamSessionId;
    private bool _webUiWarned;

    public bool IsActive
    {
        get
        {
            lock (_gate)
                return _process is { HasExited: false };
        }
    }

    public async Task<bool> TryPlayAsync(
        WindowsMpcPlayRequest request,
        IPlayerService player,
        CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);

        var options = WindowsMpcPlaybackSettings.Load(deviceStorage);
        var exePath = ResolveExePath(options.ExePath);
        if (!options.Enabled)
            return false;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            snackbar.Add(localizer["MpcExternalPlayerLaunchFailed"], K7Severity.Error);
            return false;
        }

        var session = await streamUriService.GetOrCreateSessionAsync(
            request.IndexedFileId,
            request.AudioTrackIndex,
            request.SubtitleTrackIndex,
            cancellationToken);

        var token = await streamingService.GenerateEphemeralTokenAsync(session.Id, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            snackbar.Add(localizer["MpcExternalPlayerLaunchFailed"], K7Severity.Error);
            return false;
        }

        var relative = GetIndexedFileDirectStreamQueryUriBuilder.Build(request.IndexedFileId);
        var absolute = k7ServerService.GetAbsoluteUri(relative);
        if (absolute is null)
        {
            snackbar.Add(localizer["MpcExternalPlayerLaunchFailed"], K7Severity.Error);
            return false;
        }

        var streamUrl = AppendEphemeralToken(absolute.ToString(), token);
        var startMs = MpcCommandLine.ToStartMilliseconds(request.StartPositionSeconds);
        var arguments = MpcCommandLine.Build(streamUrl, options.ExtraArgs, startMs, options.WebPort);

        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false
            });
        }
        catch
        {
            snackbar.Add(localizer["MpcExternalPlayerLaunchFailed"], K7Severity.Error);
            return false;
        }

        if (process is null)
        {
            snackbar.Add(localizer["MpcExternalPlayerLaunchFailed"], K7Severity.Error);
            return false;
        }

        lock (_gate)
        {
            _process = process;
            _streamSessionId = session.Id;
            _webUiWarned = false;
            _monitorCts = new CancellationTokenSource();
        }

        player.Source = new PlayerSource
        {
            MediaId = request.MediaId,
            StreamSessionId = session.Id,
            IndexedFileId = request.IndexedFileId,
            Url = streamUrl,
            MimeType = "application/octet-stream",
            Title = request.Title,
            CoverUrl = request.CoverUrl,
            KnownDurationSeconds = request.DurationSeconds,
            PendingSeekTime = request.StartPositionSeconds is > 0 ? request.StartPositionSeconds : null
        };

        var startSeconds = request.StartPositionSeconds is > 0 ? request.StartPositionSeconds.Value : 0;
        player.ApplyExternalClock(startSeconds, request.DurationSeconds, PlaybackState.Playing);

        var monitorCts = _monitorCts;
        _ = MonitorAsync(player, options, startMs, monitorCts!.Token);
        return true;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Process? process;
        CancellationTokenSource? monitorCts;
        Guid? sessionId;
        lock (_gate)
        {
            process = _process;
            monitorCts = _monitorCts;
            sessionId = _streamSessionId;
            _process = null;
            _monitorCts = null;
            _streamSessionId = null;
        }

        if (monitorCts is not null)
        {
            await monitorCts.CancelAsync();
            monitorCts.Dispose();
        }

        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            process.Dispose();
        }

        if (sessionId is Guid id)
        {
            try
            {
                await streamingService.RevokeEphemeralTokenAsync(id, cancellationToken);
            }
            catch
            {
            }
        }
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();

    private async Task MonitorAsync(
        IPlayerService player,
        WindowsMpcPlaybackOptions options,
        int startMilliseconds,
        CancellationToken cancellationToken)
    {
        var sawWebUi = false;
        var sawPlayback = false;
        var seekSent = startMilliseconds <= 0;
        MpcPlayerVariables? last = null;
        var warnAfter = DateTime.UtcNow + WebUiWait;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsActive)
                break;

            var snapshot = await TryReadVariablesAsync(options, cancellationToken);
            if (snapshot is not null)
            {
                sawWebUi = true;
                if (snapshot.State is PlaybackState.Playing or PlaybackState.Paused)
                    sawPlayback = true;

                // /webport brings the UI up before the file is open. Ignore Stopped until
                // MPC has actually played or K7 treats it as ended and kills the process.
                if (sawPlayback || snapshot.State != PlaybackState.Ended)
                {
                    last = snapshot;
                    if (!seekSent && snapshot.DurationSeconds > 1 && snapshot.PositionSeconds * 1000 < startMilliseconds * 0.5)
                    {
                        var percent = startMilliseconds / 10.0 / snapshot.DurationSeconds;
                        await _web.SeekPercentAsync(options.WebHost, options.WebPort, percent, cancellationToken);
                        seekSent = true;
                    }
                    else
                    {
                        seekSent = true;
                    }

                    player.ApplyExternalClock(snapshot.PositionSeconds, snapshot.DurationSeconds, snapshot.State);
                    if (snapshot.State == PlaybackState.Ended)
                        break;
                }
            }
            else if (!sawWebUi && !_webUiWarned && DateTime.UtcNow >= warnAfter)
            {
                _webUiWarned = true;
                snackbar.Add(localizer["MpcExternalPlayerWebUiUnavailable"], K7Severity.Warning);
            }

            try
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (last is not null)
            player.ApplyExternalClock(last.PositionSeconds, last.DurationSeconds, PlaybackState.Ended);
        else
            player.ApplyExternalClock(player.CurrentTime, player.Duration, PlaybackState.Ended);

        Guid? sessionId;
        lock (_gate)
        {
            sessionId = _streamSessionId;
            _streamSessionId = null;
            _process?.Dispose();
            _process = null;
        }

        if (sessionId is Guid id)
        {
            try
            {
                await streamingService.RevokeEphemeralTokenAsync(id, CancellationToken.None);
            }
            catch
            {
            }
        }
    }

    private async Task<MpcPlayerVariables?> TryReadVariablesAsync(
        WindowsMpcPlaybackOptions options,
        CancellationToken cancellationToken)
    {
        var primary = await _web.GetVariablesAsync(options.WebHost, options.WebPort, cancellationToken);
        if (primary is not null)
            return primary;

        if (string.Equals(options.WebHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
            return await _web.GetVariablesAsync("localhost", options.WebPort, cancellationToken);

        if (string.Equals(options.WebHost, "localhost", StringComparison.OrdinalIgnoreCase))
            return await _web.GetVariablesAsync("127.0.0.1", options.WebPort, cancellationToken);

        return null;
    }

    private static string? ResolveExePath(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        return MpcExeLocator.TryFind();
    }

    private static string AppendEphemeralToken(string url, string token)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}ephemeral_token={Uri.EscapeDataString(token)}";
    }
}
