using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public sealed class AmbientThemeService : IAmbientThemeService, IAsyncDisposable
{
    private const double DefaultCrossfadeSeconds = 1.2;
    private const double DefaultLeaveFadeSeconds = 1.5;

    private readonly IJSRuntime _js;
    private readonly NavigationManager _navigation;
    private readonly TimeSpan _leaveGrace;
    private readonly object _gate = new();
    private readonly DotNetObjectReference<AmbientThemeService> _jsRef;
    private readonly IThemeSongSourceResolver? _sourceResolver;
    private Guid? _currentMediaId;
    private string? _currentUrl;
    private bool _finished;
    private CancellationTokenSource? _leaveCts;
    private int _playVersion;
    private bool _disposed;

    public AmbientThemeService(IJSRuntime js, NavigationManager navigation, IThemeSongSourceResolver? sourceResolver = null)
        : this(js, navigation, leaveGrace: null, sourceResolver)
    {
    }

    /// <summary>
    /// Testable constructor with optional leave grace override.
    /// </summary>
    internal AmbientThemeService(
        IJSRuntime js,
        NavigationManager navigation,
        TimeSpan? leaveGrace,
        IThemeSongSourceResolver? sourceResolver = null)
    {
        _js = js;
        _navigation = navigation;
        _leaveGrace = leaveGrace ?? TimeSpan.FromMilliseconds(500);
        _sourceResolver = sourceResolver;
        _jsRef = DotNetObjectReference.Create(this);
        _navigation.LocationChanged += OnLocationChanged;
    }

    public Guid? CurrentMediaId
    {
        get
        {
            lock (_gate)
                return _currentMediaId;
        }
    }

    public bool IsFinished
    {
        get
        {
            lock (_gate)
                return _finished;
        }
    }

    /// <summary>
    /// Test hook that applies the same rules as <see cref="NavigationManager.LocationChanged"/>.
    /// </summary>
    internal void HandleLocationChanged(string absoluteUri) =>
        ApplyLocation(absoluteUri);

    public async Task KeepOrStartAsync(
        Guid mediaId,
        string themeUrl,
        byte[] audioBytes,
        double volume = 0.25,
        CancellationToken cancellationToken = default)
    {
        await StartCoreAsync(
            mediaId,
            themeUrl,
            audioBytes is { Length: > 0 },
            async ct =>
            {
                await _js.InvokeVoidAsync(
                    "K7.AmbientTheme.playBytes",
                    ct,
                    audioBytes,
                    volume,
                    DefaultCrossfadeSeconds,
                    themeUrl,
                    _jsRef);
                return true;
            },
            cancellationToken);
    }

    public async Task<bool> TryKeepOrStartStreamingAsync(
        Guid mediaId,
        string themeUrl,
        double volume = 0.25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(themeUrl))
            return false;

        var playableUrl = _sourceResolver?.ResolvePlayableUrl(mediaId, themeUrl);

        return await StartCoreAsync(
            mediaId,
            themeUrl,
            !string.IsNullOrWhiteSpace(playableUrl),
            ct => _js.InvokeAsync<bool>(
                "K7.AmbientTheme.playUrl",
                ct,
                playableUrl,
                volume,
                DefaultCrossfadeSeconds,
                themeUrl,
                _jsRef).AsTask(),
            cancellationToken);
    }

    // Returns true when the theme plays (or already played) for this media context; false when
    // nothing could be started, so a caller can try another source.
    private async Task<bool> StartCoreAsync(
        Guid mediaId,
        string themeUrl,
        bool hasSource,
        Func<CancellationToken, Task<bool>> playAsync,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(themeUrl))
            return false;

        CancelLeave();

        lock (_gate)
        {
            if (_currentMediaId == mediaId
                && (_finished || string.Equals(_currentUrl, themeUrl, StringComparison.Ordinal)))
            {
                // Same media context: keep playing, or keep Finished (do not restart).
                return true;
            }
        }

        if (!hasSource)
            return false;

        var version = Interlocked.Increment(ref _playVersion);

        try
        {
            await PlaybackAssetLoader.EnsureAsync(_js, cancellationToken);
            if (!await playAsync(cancellationToken))
                return false;
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            return false;
        }

        if (version != Volatile.Read(ref _playVersion))
        {
            // Watch/leave won the race: do not leave an orphan HTML5 Audio running.
            await HaltJsAsync();
            return true;
        }

        lock (_gate)
        {
            _currentMediaId = mediaId;
            _currentUrl = themeUrl;
            _finished = false;
        }

        return true;
    }

    public async Task InterruptAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        CancelLeave();
        Interlocked.Increment(ref _playVersion);

        lock (_gate)
        {
            _currentMediaId = mediaId;
            _finished = true;
        }

        try
        {
            await _js.InvokeVoidAsync("K7.AmbientTheme.fadeOut", cancellationToken, 0.4);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    public void ScheduleLeave(Guid mediaId)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            if (_currentMediaId != mediaId)
                return;

            _leaveCts?.Cancel();
            _leaveCts?.Dispose();
            cts = new CancellationTokenSource();
            _leaveCts = cts;
        }

        _ = FadeAfterLeaveGraceAsync(mediaId, cts);
    }

    public async Task FadeOutAsync(double durationSeconds = 0.5, CancellationToken cancellationToken = default)
    {
        CancelLeave();
        ClearCurrent();
        Interlocked.Increment(ref _playVersion);

        try
        {
            await _js.InvokeVoidAsync("K7.AmbientTheme.fadeOut", cancellationToken, durationSeconds);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancelLeave();
        ClearCurrent();
        Interlocked.Increment(ref _playVersion);

        try
        {
            await _js.InvokeVoidAsync("K7.AmbientTheme.stop", cancellationToken);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Called from JS when HTML5 Audio reaches natural end (no loop).
    /// </summary>
    [JSInvokable]
    public void NotifyNaturalEnded()
    {
        lock (_gate)
        {
            if (_currentMediaId is null)
                return;

            _finished = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _navigation.LocationChanged -= OnLocationChanged;
        CancelLeave();
        await StopAsync();
        _jsRef.Dispose();
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) =>
        ApplyLocation(args.Location);

    private void ApplyLocation(string absoluteUri)
    {
        if (_disposed)
            return;

        Guid? current;
        lock (_gate)
            current = _currentMediaId;

        if (current is null)
            return;

        // Media tree or casting digression: keep playing / Finished context.
        if (AmbientThemeRoutes.IsThemeHoldingRoute(absoluteUri))
        {
            CancelLeave();
            return;
        }

        ScheduleLeave(current.Value);
    }

    private async Task FadeAfterLeaveGraceAsync(Guid mediaId, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_leaveGrace, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            if (_currentMediaId != mediaId || !ReferenceEquals(_leaveCts, cts))
                return;
        }

        await FadeOutAsync(DefaultLeaveFadeSeconds, CancellationToken.None);
    }

    private void CancelLeave()
    {
        lock (_gate)
        {
            if (_leaveCts is null)
                return;

            _leaveCts.Cancel();
            _leaveCts.Dispose();
            _leaveCts = null;
        }
    }

    private async Task HaltJsAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("K7.AmbientTheme.stop", CancellationToken.None);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    private void ClearCurrent()
    {
        lock (_gate)
        {
            _currentMediaId = null;
            _currentUrl = null;
            _finished = false;
        }
    }
}
