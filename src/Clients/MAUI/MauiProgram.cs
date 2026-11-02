using CommunityToolkit.Maui;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Data;
using K7.Clients.MAUI.Interfaces;
using K7.Clients.MAUI.Services;
using K7.Clients.MAUI.Services.Authentication;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using K7.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using OpenIddict.Client;
using OpenIddict.Client.SystemIntegration;
using SkiaSharp.Views.Maui.Controls.Hosting;
using static OpenIddict.Abstractions.OpenIddictConstants;

#if WINDOWS
using K7.Clients.MAUI.Platforms.Windows;
#endif
#if WINDOWS
using LibVLCSharp.MAUI;
#endif
#if LINUX
using K7.Clients.MAUI.Platforms.Linux;
using Microsoft.Maui.Platforms.Linux.Gtk4.BlazorWebView;
using Microsoft.Maui.Platforms.Linux.Gtk4.Essentials.Hosting;
using Microsoft.Maui.Platforms.Linux.Gtk4.Hosting;
#endif

namespace K7.Clients.MAUI;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiStartupTrace.Mark("CreateMauiApp-start");
        ColdStartTrace.Mark = MauiStartupTrace.Mark;
        PerfTrace.Sink = MauiStartupTrace.WritePerf;
        // AppReady may fire at SelectProfile OnInitialized (before first paint). Do not start
        // OpenIddict there - it starved EmptyLayout ~13s on TV. Heavy work waits FirstPaint.
        AppReadySignal.OnSignaled = static () => MauiStartupTrace.Mark("AppReadySignal");
        FirstPaintSignal.OnSignaled = static () =>
        {
            MauiStartupTrace.Mark("FirstPaintSignal");
            MauiSessionBootstrap.ScheduleHeavyWorkAfterFirstPaint();
        };
#if LINUX
        // Static Essentials (Preferences, FileSystem, DeviceInfo) must work before DI exists.
        LinuxEssentialsDefaults.ApplyEarly();
#endif
        // Native chrome over LibVLC Direct Play and Video.js HLS (Windows and Linux alike).
        MauiNativeVideoChrome.EnableForNativeMediaElementHosts();

#if WINDOWS
        // Capture JSException.Message (VS only shows "Exception thrown" without details).
        // Also reports a rate-limited sample to the server via IClientErrorReporter once DI is ready.
        JsExceptionDebugListener.Install();
        WindowsNativeProtocol.EnsureRegistered();
#endif
        NativeAuthTrace.Install();

        var builder = MauiApp.CreateBuilder();
        builder
#if LINUX
            // GTK4 backend + labs Essentials. MediaElement / SkiaSharp have no GTK handlers:
            // video and music play inside WebKitGTK (Video.js / audioplayer.js).
            .UseMauiAppLinuxGtk4<App>()
            .AddLinuxGtk4Essentials()
#else
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
#endif
#if WINDOWS
            .UseLibVLCSharp()
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("Phosphor.ttf", "Phosphor");
            });

#if LINUX
        builder.Services.AddBlazorWebView();
        builder.Services.AddLinuxGtk4BlazorWebView();
#else
        builder.Services.AddMauiBlazorWebView();
#endif

        builder.Services.AddSingleton<AuthSessionKeeper>();
        builder.ConfigureLifecycleEvents(events =>
        {
#if WINDOWS
            events.AddWindows(windows =>
            {
                windows.OnWindowCreated(nativeWindow =>
                {
                    WindowGeometryPersistence.Attach(nativeWindow);
                    WindowsProtocolActivation.Attach();
                    AttachMusicSessionFlushOnClose(nativeWindow);
                });
            });
#elif ANDROID
            events.AddAndroid(android =>
            {
                android.OnPause(_ =>
                {
                    AppLifecycleGate.SetForeground(false);
                    if (IPlatformApplication.Current?.Services is { } services)
                        services.GetService<MusicSessionPersistenceService>()?.Flush();
                });
                android.OnResume(_ =>
                {
                    AppLifecycleGate.SetForeground(true);
                    if (IPlatformApplication.Current?.Services is { } services)
                        services.GetService<AuthSessionKeeper>()?.OnAppResumed();
                });
            });
#elif IOS || MACCATALYST
            events.AddiOS(ios =>
            {
                ios.OnResignActivation(_ =>
                {
                    AppLifecycleGate.SetForeground(false);
                    if (IPlatformApplication.Current?.Services is { } services)
                        services.GetService<MusicSessionPersistenceService>()?.Flush();
                });
                ios.OnActivated(_ =>
                {
                    AppLifecycleGate.SetForeground(true);
                    if (IPlatformApplication.Current?.Services is { } services)
                        services.GetService<AuthSessionKeeper>()?.OnAppResumed();
                });
            });
#elif LINUX
            events.AddGtk(gtk =>
            {
                // Services exist here (IPlatformApplication.Current.Services). The remaining
                // static Essentials defaults and the k7:// listener need them.
                gtk.OnMauiApplicationCreated(_ =>
                {
                    if (IPlatformApplication.Current?.Services is { } services)
                        LinuxEssentialsDefaults.ApplyFromServices(services);
                    LinuxProtocolActivation.Attach();
                });
            });
#endif
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
        builder.Logging.AddFilter("Microsoft.AspNetCore.Components.WebView", LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft.JSInterop", LogLevel.Trace);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Components", LogLevel.Information);
#endif

        // Blazor errors outside an ErrorBoundary were invisible in Release (no log sink).
        builder.Services.AddSingleton<ILoggerProvider, BlazorErrorLoggerProvider>();

        // https://github.com/dotnet/maui/issues/14185
        // https://github.com/microsoft/microsoft-ui-xaml/issues/6527
        builder.ConfigureMauiHandlers(handlers =>
        {
#if WINDOWS
            handlers.AddHandler<BlazorWebView, Platforms.Windows.TransparentBlazorWebViewHandler>();
#elif ANDROID
            handlers.AddHandler<BlazorWebView, Platforms.Android.TransparentBlazorWebViewHandler>();
#elif IOS
            handlers.AddHandler<BlazorWebView, Platforms.iOS.TransparentBlazorWebViewHandler>();
#elif LINUX
            handlers.AddHandler<BlazorWebView, Microsoft.Maui.Platforms.Linux.Gtk4.BlazorWebView.BlazorWebViewHandler>();
            handlers.AddHandler<LinuxVlcVideoView, LinuxVlcVideoViewHandler>();
            handlers.AddHandler<LinuxLottieView, LinuxLottieViewHandler>();
            // Labs fixes for the native video overlay: non-cascading InputTransparent layouts
            // stay hit-testable, BoxView paints gradients instead of an opaque placeholder.
            handlers.AddHandler<Layout, LinuxLayoutHandler>();
            handlers.AddHandler<BoxView, LinuxBoxViewHandler>();
            handlers.AddHandler<ScrollView, LinuxScrollViewHandler>();
            LinuxLabelTextShadow.Install();
#endif
        });

        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.ConfigurePlatformServices();
        builder.Services.TryAddSingleton<ILocalPathPicker, NullLocalPathPicker>();

        builder.Services.AddSingleton<SidebarService>();
        builder.Services.AddSingleton<BackButtonService>();
        builder.Services.AddSingleton<ThemeService>();

        builder.Services.AddTransient<PerfTraceHttpHandler>();
        builder.Services.AddTransient<AuthenticationDelegatingHandler>();
        builder.Services.AddHttpClient(nameof(K7ServerService))
            .AddHttpMessageHandler<PerfTraceHttpHandler>()
            .AddHttpMessageHandler<AuthenticationDelegatingHandler>()
#if !IOS && !MACCATALYST
            // Android: HttpURLConnection (AndroidMessageHandler) only allows standard HTTP methods;
            // SocketsHttpHandler supports QUERY and other custom methods. Every socket-based head
            // also asks for and decodes Brotli/gzip API responses (Apple's NSUrlSession does it itself).
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            })
#endif
            ;
        builder.Services.AddSingleton<K7ServerService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient(nameof(K7ServerService));
            return new K7ServerService(client);
        });
        builder.Services.AddSingleton<IK7ServerService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IMediaService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<ILibraryService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IPlaylistService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<ICollectionService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<ISearchService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IStreamingService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IDeviceApiService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IUserAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IRatingService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IReviewService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<ISocialUserService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IServerInfoService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IBackgroundTaskService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IDiagnosticsService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IUserPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IMusicSessionApi>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IServerPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IApiKeyAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IClientAppPasswordUserService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IMusicIntelligenceAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IScrobblingAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IScrobblingUserService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<ITranscodeAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IMusicIntelligenceClientService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IDownloadService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<INotificationAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IFederationService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<K7ServerManagerService>();
        builder.Services.AddSingleton<IServerConnectionService>(sp => sp.GetRequiredService<K7ServerManagerService>());

        builder.Services.AddSingleton<WebViewJsBridge>();
        builder.Services.AddSingleton<IDeviceService, DeviceService>();
        builder.Services.AddSingleton<IExternalLinkService, MauiExternalLinkService>();
#if !ANDROID
        builder.Services.AddSingleton<ISoftKeyboardService, NoOpSoftKeyboardService>();
#endif
        builder.Services.AddSingleton<IAppExitService, AppExitService>();
        builder.Services.AddSingleton<IBrightnessService, BrightnessService>();
        builder.Services.AddSingleton<IVolumeService, VolumeService>();
        builder.Services.AddSingleton<IStreamUriService, StreamUriService>();
        builder.Services.AddSingleton<IPlayerService, PlayerService>();
        builder.Services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
#if ANDROID
        // Theme songs stream through the WebView interception (BlazorPage.OnWebResourceRequested).
        builder.Services.AddSingleton<IThemeSongSourceResolver, ThemeSongAuthProxy>();
#endif
        // Scoped: IJSRuntime is scoped in Blazor Hybrid; a singleton would capture a dead runtime.
        builder.Services.AddScoped<IAmbientThemeService, AmbientThemeService>();
        builder.Services.AddSingleton<ISleepTimerService, SleepTimerService>();
        builder.Services.AddSingleton<AutoplayService>();
        builder.Services.AddSingleton<IMusicRadioPlaybackService, MusicRadioPlaybackService>();
        builder.Services.AddSingleton<IMediaPlayerService, MediaPlayerService>();
        builder.Services.AddSingleton<IMediaStreamSession, MediaSessionService>();
        builder.Services.AddSingleton<IMediaBrowseService, MediaBrowseService>();
        builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();
        builder.Services.AddSingleton<IMusicSessionStore, FileMusicSessionStore>();
        builder.Services.AddSingleton<IPageFilterStorage, PageFilterStorage>();
        builder.Services.AddSingleton<ISharedProfileApi>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<ISharedProfileLocalCache, SharedProfileLocalCache>();
        builder.Services.AddSingleton<ISharedProfileService, SharedProfileService>();
        builder.Services.AddSingleton<ISharedProfileSessionService, SharedProfileSessionService>();
        builder.Services.AddSingleton<ISharedProfileDevicePinService, SharedProfileDevicePinService>();
        builder.Services.AddSingleton<ILocalUserService, LocalUserService>();
        builder.Services.AddSingleton<K7HubClient>();
        builder.Services.AddSingleton<IVideoPlayerSettingsHubEvents>(sp => sp.GetRequiredService<K7HubClient>());
        builder.Services.AddSingleton<ICustomNavHubEvents>(sp => sp.GetRequiredService<K7HubClient>());
        builder.Services.AddSingleton<IUserRatingSync, UserRatingSync>();
        builder.Services.AddSingleton<IVideoPlayerUxSettingsSync, VideoPlayerUxSettingsSync>();
        builder.Services.AddSingleton(new MediaCacheStore(maxEntries: 32));
        builder.Services.AddSingleton<IHomeNavigationState, HomeNavigationState>();
        builder.Services.AddSingleton<IHubFocusNavigationState, HubFocusNavigationState>();
        builder.Services.AddSingleton<IHomeFeedStore, HomeFeedStore>();
        builder.Services.AddSingleton<IFeedHubHostService, FeedHubHostService>();
        builder.Services.AddSingleton<IMediaBrowseHubCoordinator, MediaBrowseHubCoordinator>();
        builder.Services.AddSingleton<IExploreGroupStore, ExploreGroupStore>();
        builder.Services.AddSingleton<ICustomNavStore, CustomNavStore>();
        builder.Services.AddSingleton<ILibraryGroupContextStore, LibraryGroupContextStore>();
        builder.Services.AddSingleton<PlaybackProgressTracker>();
        builder.Services.AddSingleton<AudioPlaybackProgressTracker>();

        // Offline playback services
        builder.Services.AddDbContextFactory<Data.OfflineMediaDbContext>(options =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "k7-offline.db");
            options.UseSqlite($"Data Source={dbPath}");
        });
        builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();
        builder.Services.AddSingleton<IOfflineMediaStore, OfflineMediaStore>();
        builder.Services.AddSingleton<IPlaybackJournal, PlaybackJournal>();
#if !ANDROID
        builder.Services.AddSingleton<IDownloadKeepAlive, NoOpDownloadKeepAlive>();
#endif
        builder.Services.AddSingleton<IDownloadManager, Services.DownloadManager>();
        builder.Services.AddSingleton<IMusicCacheService, MusicCacheService>();
        builder.Services.AddSingleton<IPlaybackSyncService, PlaybackSyncService>();

        builder.Services.AddSingleton<K7DialogService>();
        builder.Services.AddSingleton<IK7DialogService>(sp => sp.GetRequiredService<K7DialogService>());
        builder.Services.AddSingleton<K7SnackbarService>();
        builder.Services.AddSingleton<IK7Snackbar>(sp => sp.GetRequiredService<K7SnackbarService>());
        builder.Services.AddSingleton<MediaCardContextMenuService>();
        builder.Services.AddSingleton<IMediaCardContextMenuService>(sp => sp.GetRequiredService<MediaCardContextMenuService>());
        builder.Services.AddSingleton<IClientErrorReporter, ClientErrorReporter>();
        // Scoped: IJSRuntime is scoped in Blazor Hybrid; a singleton would capture a dead runtime.
        builder.Services.AddScoped<ISpatialNavService, SpatialNavService>();
        builder.Services.AddScoped<SoftKeyboardJsBridge>();
#if WINDOWS || LINUX
        // Video.js / audioplayer.js fetch K7 streams through HttpClient (auth + no CORS).
        builder.Services.AddScoped<IWindowsStreamFetchJsBridge, Playback.WindowsStreamFetchJsBridge>();
#else
        builder.Services.AddScoped<IWindowsStreamFetchJsBridge, NoOpWindowsStreamFetchJsBridge>();
#endif
        builder.Services.AddSingleton<ICastOrchestrationService, CastOrchestrationService>();
        builder.Services.AddSingleton<IUiDispatcher, MauiUiDispatcher>();
        builder.Services.AddSingleton<IExternalPlayerPolicy, ExternalPlayerPolicy>();
        builder.Services.AddSingleton<RemotePlaybackHandler>();
        builder.Services.AddSingleton<RemoteControlService>();
        builder.Services.AddSingleton<IRemoteControlService>(sp => sp.GetRequiredService<RemoteControlService>());
        builder.Services.AddSingleton<RemotePlaybackLauncher>();
        builder.Services.AddSingleton<NowPlayingService>();
        builder.Services.AddSingleton<MusicSessionPersistenceService>();
        builder.Services.AddSingleton<IMusicSessionPersistence>(sp => sp.GetRequiredService<MusicSessionPersistenceService>());
        builder.Services.AddSingleton<SyncPlayService>();
        builder.Services.AddSingleton<ISyncPlayService>(sp => sp.GetRequiredService<SyncPlayService>());
        builder.Services.AddSingleton<ISyncPlayMediaLoader, SyncPlayMediaLoader>();
        builder.Services.AddSingleton<SyncPlayPlaybackHandler>();

        var serverUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);
        ConfigureOpenIddict(builder.Services, serverUrl);

        builder.Services.AddAuthorizationCore();
        builder.Services.AddSingleton<ICustomAuthenticationStateProvider, CustomAuthenticationStateProvider>();
        builder.Services.AddSingleton(sp => (AuthenticationStateProvider)sp.GetRequiredService<ICustomAuthenticationStateProvider>());
        builder.Services.AddSingleton<IFeatureAccessService, FeatureAccessService>();

        System.Diagnostics.Debug.WriteLine("K7 MAUI - Calling builder.Build()");
        MauiStartupTrace.Mark("builder.Build-start");
        var app = builder.Build();
        MauiStartupTrace.Mark("builder.Build-end");
        NativeAuthTrace.Configure(app.Services);
#if WINDOWS
        WindowsProtocolActivation.Attach();
#elif LINUX
        // Socket + callback file watcher as early as possible; URIs queue until services exist.
        LinuxProtocolActivation.StartListening();
#endif
        System.Diagnostics.Debug.WriteLine("K7 MAUI - builder.Build() completed");

        // OfflineDb / OpenIddict EnsureCreated + playback warmups: ScheduleHeavyWorkAfterFirstPaint
        // (AppReady) or MauiSessionBootstrap.EnsureReadyAsync (Android Auto headless).

        System.Diagnostics.Debug.WriteLine("K7 MAUI - CreateMauiApp returning");
        MauiStartupTrace.Mark("CreateMauiApp-end");
        return app;
    }

    private static void ConfigureOpenIddict(IServiceCollection services, string? serverUrl)
    {
        string dbPath;
        try
        {
            dbPath = Path.Combine(FileSystem.AppDataDirectory, "k7-openiddict.db");
            System.Diagnostics.Debug.WriteLine($"K7 MAUI - OpenIddict DB path: {dbPath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"K7 MAUI - FileSystem.AppDataDirectory failed: {ex}");
            dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "k7-openiddict.db");
            System.Diagnostics.Debug.WriteLine($"K7 MAUI - Fallback DB path: {dbPath}");
        }

        System.Diagnostics.Debug.WriteLine("K7 MAUI - Initializing SQLitePCL");
        SQLitePCL.Batteries_V2.Init();
        System.Diagnostics.Debug.WriteLine("K7 MAUI - Registering DbContext");
        services.AddDbContext<OpenIddictDbContext>(options =>
        {
            options.UseSqlite($"Filename={dbPath}");
            options.UseOpenIddict();
        });

        System.Diagnostics.Debug.WriteLine("K7 MAUI - Registering OpenIddict");
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<OpenIddictDbContext>();
            })
            .AddClient(options =>
            {
                options.AllowAuthorizationCodeFlow()
                       .AllowDeviceAuthorizationFlow()
                       .AllowRefreshTokenFlow();

#if ANDROID || IOS
                // Persisted symmetric keys: no RSA generation on the cold-start path, and the
                // state token of a browser login survives a process kill before the callback.
                OpenIddictClientKeys.Register(options);
#else
                // Development certificates live in the CurrentUser X.509 store and often
                // break after wiping LocalState. Prefer ephemeral keys on desktop too.
                options.AddEphemeralEncryptionKey()
                       .AddEphemeralSigningKey();
#endif

                // Required whenever authorization code flow is enabled, even before a
                // server URL/registration exists (first-run SetupPage). Without this,
                // IOptions validation throws SR.ID0356 on first CurrentValue access.
                // Windows uses the same custom scheme as mobile so the system browser
                // returns the code to the running app instead of http://localhost.
#if LINUX
                // Both endpoints stay registered: k7:// is the default, the loopback listener
                // is opt-in via K7_AUTH_REDIRECT=loopback.
                options.SetRedirectionEndpointUris(
                    Linux.LinuxAuthRedirect.CustomSchemeUri,
                    Linux.LinuxAuthRedirect.LoopbackUri);
#else
                options.SetRedirectionEndpointUris(new Uri("k7://callback/login", UriKind.Absolute));
#endif

                options.UseSystemIntegration();
                options.UseSystemNetHttp()
                       .SetProductInformation(typeof(MauiProgram).Assembly);

                options.AddEventHandler(LoginCallbackResponseHandler.Descriptor);

                if (!string.IsNullOrEmpty(serverUrl))
                {
                    options.AddRegistration(CreateK7Registration(serverUrl));
                }
            });

        System.Diagnostics.Debug.WriteLine("K7 MAUI - Registering IHostEnvironment + IHostApplicationLifetime");
        // MAUI doesn't support IHostedService/IHostApplicationLifetime natively.
        // See https://github.com/dotnet/maui/issues/2244
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment
        {
            ApplicationName = typeof(MauiProgram).Assembly.GetName().Name!
        });

        services.AddSingleton<IHostApplicationLifetime, MauiHostApplicationLifetime>();

#if !ANDROID
        // Resolve the SAME singleton instances that OpenIddict registered as IHostedService,
        // so that handlers (e.g. AttachDynamicPortToRedirectUri) see the started instances.
        services.AddSingleton<IMauiInitializeService>(static provider =>
        {
            var service = provider.GetServices<IHostedService>()
                .OfType<OpenIddictClientSystemIntegrationActivationHandler>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("OpenIddict activation handler is not registered.");
            return new MauiHostedServiceAdapter(service, provider.GetRequiredService<IHostApplicationLifetime>());
        });

        services.AddSingleton<IMauiInitializeService>(static provider =>
        {
            var service = provider.GetServices<IHostedService>()
                .OfType<OpenIddictClientSystemIntegrationHttpListener>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("OpenIddict HTTP listener is not registered.");
            return new MauiHostedServiceAdapter(service, provider.GetRequiredService<IHostApplicationLifetime>());
        });

        services.AddSingleton<IMauiInitializeService>(static provider =>
        {
            var service = provider.GetServices<IHostedService>()
                .OfType<OpenIddictClientSystemIntegrationPipeListener>()
                .FirstOrDefault()
                ?? throw new InvalidOperationException("OpenIddict pipe listener is not registered.");
            return new MauiHostedServiceAdapter(service, provider.GetRequiredService<IHostApplicationLifetime>());
        });
#endif

        services.AddScoped<IMauiInitializeScopedService, MauiDatabaseInitializer>();
    }

    /// <summary>
    /// Redirect URI sent on the authorize request: the custom scheme everywhere, except Linux
    /// with <c>K7_AUTH_REDIRECT=loopback</c> (see <c>LinuxAuthRedirect</c>).
    /// </summary>
    internal static Uri NativeRedirectUri =>
#if LINUX
        Linux.LinuxAuthRedirect.Resolve(Environment.GetEnvironmentVariable(Linux.LinuxAuthRedirect.EnvironmentVariable));
#else
        new Uri("k7://callback/login", UriKind.Absolute);
#endif

    internal static OpenIddictClientRegistration CreateK7Registration(string serverUrl)
    {
        return new OpenIddictClientRegistration
        {
            Issuer = new Uri(serverUrl, UriKind.Absolute),
            ProviderName = "K7",
            RegistrationId = $"K7:{serverUrl}",
            ClientId = "k7-native",
            RedirectUri = NativeRedirectUri,
            Scopes = { Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, "api" }
        };
    }

    static partial void ConfigurePlatformServices(this IServiceCollection services);

#if WINDOWS
    private static void AttachMusicSessionFlushOnClose(Microsoft.UI.Xaml.Window nativeWindow)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Closing += (_, _) =>
        {
            var persistence = IPlatformApplication.Current?.Services
                .GetService<MusicSessionPersistenceService>();
            if (persistence is null)
                return;

            try
            {
                Task.Run(() => persistence.FlushAsync()).Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception)
            {
            }
        };
    }
#endif
}
