using System.Diagnostics;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Data;
using K7.Clients.MAUI.Services.Authentication;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.MAUI.Services;

/// <summary>
/// Server URL + session restore for both the phone window and headless car playback.
/// Android Auto binds <c>K7MediaLibraryService</c> without the phone UI window.
/// </summary>
internal static class MauiSessionBootstrap
{
    private static readonly object HeavyGate = new();
    private static Task? _heavyTask;

    /// <summary>
    /// Sets HttpClient.BaseAddress only. Does not touch OpenIddict (safe on UI thread).
    /// Returns true when the address actually changed.
    /// </summary>
    public static bool ApplyHttpBaseAddress(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var k7ServerUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);
        if (string.IsNullOrEmpty(k7ServerUrl))
            return false;

        var k7 = services.GetService<IK7ServerService>();
        if (k7 is null)
            return false;

        var newUri = new Uri(k7ServerUrl);
        if (k7.HttpClient.BaseAddress?.OriginalString == newUri.OriginalString)
            return false;

        try
        {
            k7.HttpClient.BaseAddress = newUri;
            return true;
        }
        catch (InvalidOperationException)
        {
            // Requests already started.
            return false;
        }
    }

    /// <summary>
    /// Full URL + OpenIddict registration. Resolving <see cref="K7ServerManagerService"/>
    /// pulls OpenIddictClientService (multi-second on Android TV) - call off UI thread.
    /// </summary>
    public static void ApplyServerUrl(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var k7ServerUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);
        if (string.IsNullOrEmpty(k7ServerUrl))
            return;

        ApplyHttpBaseAddress(services);

        // First resolve builds the OpenIddict client options (handlers, keys): the markers
        // split that cost from the registration itself.
        MauiStartupTrace.Mark("ServerUrl-manager-resolve-start");
        var manager = services.GetService<K7ServerManagerService>();
        MauiStartupTrace.Mark("ServerUrl-manager-resolved");
        if (manager is null)
            return;

        try
        {
            manager.UpdateBaseAddress(k7ServerUrl);
            manager.EnsureOpenIddictRegistration(k7ServerUrl);
            MauiStartupTrace.Mark("ServerUrl-registration-end");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - Session bootstrap UpdateBaseAddress failed: {ex}");
        }
    }

    public static void StartRestore(IServiceProvider? services = null)
    {
        services ??= IPlatformApplication.Current?.Services;
        if (services is null)
            return;

        // Cheap only: do not touch OpenIddict / EF here. Heavy work waits for first paint
        // (ScheduleHeavyWorkAfterFirstPaint) so it does not contend with BlazorWebView boot.
        ApplyHttpBaseAddress(services);

        // SecureStorage reads for the provisional sign-in, off the main thread and well before
        // Blazor's first GetAuthenticationStateAsync (main thread, during first paint).
        _ = Task.Run(() =>
        {
            try
            {
                if (services.GetService<ICustomAuthenticationStateProvider>() is CustomAuthenticationStateProvider custom)
                    custom.PrefetchStoredSession();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"K7 MAUI - Stored session prefetch failed: {ex}");
            }
        });
    }

    /// <summary>
    /// After first paint: OpenIddict DB, offline DB, server registration, session restore, playback warmups.
    /// </summary>
    public static void ScheduleHeavyWorkAfterFirstPaint(IServiceProvider? services = null)
    {
        services ??= IPlatformApplication.Current?.Services;
        if (services is null)
            return;

        _ = EnsureHeavyWorkAsync(services);
    }

    /// <summary>
    /// Runs the heavy work once on a worker thread and returns the shared task. Later callers
    /// chain a cheap refresh (server URL + auth restore) behind it, still off their own thread.
    /// Never run this inline on the main thread: resolving K7ServerManagerService (OpenIddict
    /// options, ephemeral RSA keys) took 24 s on a 32-bit TV box and ended in an ANR when
    /// K7MediaLibraryService.OnCreate did it.
    /// </summary>
    public static Task EnsureHeavyWorkAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        lock (HeavyGate)
        {
            _heavyTask = _heavyTask is null
                ? Task.Run(() => RunHeavyWork(services))
                : _heavyTask.ContinueWith(_ => RefreshAfterHeavyWork(services), TaskScheduler.Default);
            return _heavyTask;
        }
    }

    private static void RefreshAfterHeavyWork(IServiceProvider services)
    {
        try
        {
            ApplyServerUrl(services);
            if (services.GetService<ICustomAuthenticationStateProvider>() is CustomAuthenticationStateProvider custom)
                custom.EnsureRestoreStarted();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - Session bootstrap refresh failed: {ex}");
        }
    }

    /// <summary>
    /// OpenIddict SQLite, server registration, auth restore, offline DB, playback handlers.
    /// Worker thread only (see <see cref="EnsureHeavyWorkAsync"/>).
    /// </summary>
    private static void RunHeavyWork(IServiceProvider services)
    {
        MauiStartupTrace.Mark("DeferredHeavy-start");
        try
        {
            var offlineFactory = services.GetService<IDbContextFactory<OfflineMediaDbContext>>();
            if (offlineFactory is not null)
                OfflineDbBootstrap.Start(offlineFactory);
            MauiStartupTrace.Mark("DeferredHeavy-offline-db");

            OpenIddictDbBootstrap.Start(services);
            MauiStartupTrace.Mark("DeferredHeavy-openiddict-db");

            ApplyServerUrl(services);
            MauiStartupTrace.Mark("DeferredHeavy-server-url");

            if (services.GetService<ICustomAuthenticationStateProvider>() is CustomAuthenticationStateProvider custom)
                custom.EnsureRestoreStarted();
            MauiStartupTrace.Mark("DeferredHeavy-auth-restore");

            try
            {
                services.GetRequiredService<AudioPlaybackProgressTracker>();
                services.GetRequiredService<RemotePlaybackHandler>();
                services.GetRequiredService<MusicSessionPersistenceService>();
                services.GetRequiredService<IPlaybackSyncService>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("K7 MAUI - playback handler init failed: " + ex);
            }

            MauiStartupTrace.Mark("DeferredHeavy-end");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - Deferred heavy startup failed: {ex}");
        }
    }

    public static async Task EnsureReadyAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Call from a worker thread (K7MediaLibraryService does Task.Run): the continuations
        // below follow the caller's context, and on the main thread the first HTTP / JSON /
        // device-init run JITted there for 2.4 s on a 32-bit TV box.
        MauiStartupTrace.MarkRepeat("Headless-ready-start");

        // MediaLibrary can bind during interactive cold start. Wait for Blazor first paint
        // (not AppReady-at-init) so OpenIddict/EF do not contend with shell JIT.
        await WaitForInteractiveFirstPaintIfNeededAsync(cancellationToken);

        // Shared worker-thread task: the caller may be K7MediaLibraryService.OnCreate on the
        // main thread, which must not resolve OpenIddict itself.
        await EnsureHeavyWorkAsync(services).WaitAsync(cancellationToken);
        MauiStartupTrace.MarkRepeat("Headless-heavy-done");

        if (services.GetService<ICustomAuthenticationStateProvider>() is CustomAuthenticationStateProvider custom)
            await custom.EnsureReadyForHeadlessPlaybackAsync(cancellationToken);
        else if (services.GetService<AuthenticationStateProvider>() is { } auth)
            await auth.GetAuthenticationStateAsync();
        MauiStartupTrace.MarkRepeat("Headless-auth-done");

        services.GetService<AuthSessionKeeper>()?.OnAppResumed();

        var authProvider = services.GetService<AuthenticationStateProvider>();
        if (authProvider is null)
            return;

        var state = await authProvider.GetAuthenticationStateAsync().WaitAsync(cancellationToken);
        var userId = state.User.Identity?.IsAuthenticated == true
            ? AuthIdentity.GetUserId(state.User)
            : null;
        var onlineUserId = AuthIdentity.IsOnlineAuthenticated(state.User) ? userId : null;
        if (onlineUserId is not null)
            await DeviceInitializer.InitializeDeviceAsync(services, onlineUserId);
        MauiStartupTrace.MarkRepeat("Headless-device-done");

        await EnableHeadlessPlaybackReportingAsync(services, onlineUserId, cancellationToken);
        MauiStartupTrace.MarkRepeat("Headless-reporting-done");

        if (userId is null)
            return;

        var musicSessions = services.GetService<MusicSessionPersistenceService>();
        if (musicSessions is not null)
        {
            // Queue restore raises IAudioPlayerService events: keep it on the main thread (the
            // Blazor dispatcher), like the interactive MainLayout path.
            await MainThread.InvokeOnMainThreadAsync(() => musicSessions.BindAndRestoreAsync(userId, cancellationToken));
        }

        MauiStartupTrace.MarkRepeat("Headless-music-done");
    }

    private static async Task WaitForInteractiveFirstPaintIfNeededAsync(CancellationToken cancellationToken)
    {
        if (FirstPaintSignal.IsSignaled)
            return;

        // Headless Auto/CarPlay: no MAUI window yet - do not wait.
        var windows = Application.Current?.Windows;
        if (windows is null || windows.Count == 0)
            return;

        // A window that is not on screen (phone locked in a pocket while Android Auto binds
        // the service, activity recreated in the background) does not paint: waiting for it
        // stalled every car browse and play request for the whole timeout.
        if (!AppLifecycleGate.IsForeground)
            return;

        // The long wait protects the TV cold start (first paint can take 10 s+ there).
        var timeout = DeviceInfo.Idiom == DeviceIdiom.TV
            ? TimeSpan.FromSeconds(30)
            : TimeSpan.FromSeconds(6);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            await FirstPaintSignal.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Proceed with restore anyway (splash timeout / hung WebView).
        }
    }

    private static async Task EnableHeadlessPlaybackReportingAsync(
        IServiceProvider services,
        string? userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId))
            return;

        // Only signal when there is no interactive UI (headless Auto). Otherwise EmptyLayout
        // / MainLayout own AppReady after first paint - early Signal dismissed splash onto black.
        var windows = Application.Current?.Windows;
        if (windows is null || windows.Count == 0)
            AppReadySignal.Signal();

        var features = services.GetService<IFeatureAccessService>();
        var tracker = services.GetService<AudioPlaybackProgressTracker>();
        if (features is not null && tracker is not null)
        {
            var canReport = await features.HasCapabilityAsync(Capability.CanReportPlaybackProgress);
            tracker.SetCanReport(canReport);
        }

        var sync = services.GetService<IPlaybackSyncService>();
        if (sync is not null)
            sync.SyncPendingEventsAsync(cancellationToken).FireAndForget();
    }
}
