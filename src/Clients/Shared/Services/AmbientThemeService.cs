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
    private Guid? _currentMediaId;
    private string? _currentUrl;
    private bool _finished;
    private CancellationTokenSource? _leaveCts;
    private int _playVersion;
    private bool _disposed;

    public AmbientThemeService(IJSRuntime js, NavigationManager navigation)
        : this(js, navigation, leaveGrace: null)
    {
    }

    /// <summary>
    /// Testable constructor with optional leave grace override.
    /// </summary>
    internal AmbientThemeService(IJSRuntime js, NavigationManager navigation, TimeSpan? leaveGrace)
    {
        _js = js;
        _navigation = navigation;
        _leaveGrace = leaveGrace ?? TimeSpan.FromMilliseconds(500);
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
        if (string.IsNullOrWhiteSpace(themeUrl))
            return;

        CancelLeave();

        lock (_gate)
        {
            if (_currentMediaId == mediaId
                && string.Equals(_currentUrl, themeUrl, StringComparison.Ordinal))
            {
                // Same media context: keep playing, or keep Finished (do not restart).
                return;
            }
        }

        if (audioBytes is not { Length: > 0 })
            return;

        var version = Interlocked.Increment(ref _playVersion);

        try
        {
            await _js.InvokeVoidAsync(
                "K7.AmbientTheme.playBytes",
                cancellationToken,
                audioBytes,
                volume,
                DefaultCrossfadeSeconds,
                themeUrl,
                _jsRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            return;
        }

        if (version != Volatile.Read(ref _playVersion))
            return;

        lock (_gate)
        {
            _currentMediaId = mediaId;
            _currentUrl = themeUrl;
            _finished = false;
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
