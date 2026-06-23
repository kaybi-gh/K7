using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Data;
using K7.Clients.MAUI.Services;
using K7.Clients.MAUI.Services.Authentication;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
using K7.Clients.MAUI.Interfaces;
using K7.Shared.Interfaces;
using K7.Shared.Services;
using Microsoft.AspNetCore.Components.WebView.Maui;
using OpenIddict.Client;
using OpenIddict.Client.SystemIntegration;
using static OpenIddict.Abstractions.OpenIddictConstants;

#if WINDOWS
using K7.Clients.MAUI.Platforms.Windows;
#endif

namespace K7.Clients.MAUI;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseSkiaSharp()
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

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
#endif
        });

        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.ConfigurePlatformServices();

        builder.Services.AddSingleton<SidebarService>();
        builder.Services.AddSingleton<BackButtonService>();
        builder.Services.AddSingleton<ThemeService>();

        builder.Services.AddTransient<AuthenticationDelegatingHandler>();
        builder.Services.AddHttpClient(nameof(K7ServerService))
            .AddHttpMessageHandler<AuthenticationDelegatingHandler>()
#if ANDROID
            .ConfigurePrimaryHttpMessageHandler(() => new Xamarin.Android.Net.AndroidMessageHandler())
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
        builder.Services.AddSingleton<IServerInfoService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IBackgroundTaskService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IDiagnosticsService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IUserPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IServerPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IApiKeyAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IAudioMuseAiAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IAudioMuseAiClientService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IDownloadService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<INotificationAdminService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<IFederationService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddSingleton<K7ServerManagerService>();
        builder.Services.AddSingleton<IServerConnectionService>(sp => sp.GetRequiredService<K7ServerManagerService>());

        builder.Services.AddSingleton<IDeviceService, DeviceService>();
        builder.Services.AddSingleton<IBrightnessService, BrightnessService>();
        builder.Services.AddSingleton<IVolumeService, VolumeService>();
        builder.Services.AddSingleton<IStreamUriService, StreamUriService>();
        builder.Services.AddSingleton<IPlayerService, PlayerService>();
        builder.Services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
        builder.Services.AddSingleton<ISleepTimerService, SleepTimerService>();
        builder.Services.AddSingleton<AutoplayService>();
        builder.Services.AddSingleton<IMediaPlayerService, MediaPlayerService>();
        builder.Services.AddSingleton<IMediaStreamSession, MediaSessionService>();
        builder.Services.AddSingleton<IMediaBrowseService, MediaBrowseService>();
        builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();
        builder.Services.AddSingleton<ILocalUserService, LocalUserService>();
        builder.Services.AddSingleton<K7HubClient>();
        builder.Services.AddSingleton(new MediaCacheStore(maxEntries: 32));
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
        builder.Services.AddSingleton<IDownloadManager, Services.DownloadManager>();
        builder.Services.AddSingleton<IMusicCacheService, MusicCacheService>();
        builder.Services.AddSingleton<PlaybackSyncService>();

        builder.Services.AddSingleton<K7DialogService>();
        builder.Services.AddSingleton<IK7DialogService>(sp => sp.GetRequiredService<K7DialogService>());
        builder.Services.AddSingleton<K7SnackbarService>();
        builder.Services.AddSingleton<IK7Snackbar>(sp => sp.GetRequiredService<K7SnackbarService>());
        builder.Services.AddSingleton<IClientErrorReporter, ClientErrorReporter>();
        builder.Services.AddSingleton<ISpatialNavService, SpatialNavService>();
        builder.Services.AddSingleton<ICastOrchestrationService, CastOrchestrationService>();
        builder.Services.AddSingleton<RemotePlaybackHandler>();
        builder.Services.AddSingleton<RemoteControlService>();
        builder.Services.AddSingleton<IRemoteControlService>(sp => sp.GetRequiredService<RemoteControlService>());
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
        var app = builder.Build();
        System.Diagnostics.Debug.WriteLine("K7 MAUI - builder.Build() completed");

        app.Services.GetRequiredService<AudioPlaybackProgressTracker>();
        app.Services.GetRequiredService<RemotePlaybackHandler>();

        // Ensure offline DB is created
        var offlineDbFactory = app.Services.GetRequiredService<IDbContextFactory<OfflineMediaDbContext>>();
        using (var db = offlineDbFactory.CreateDbContext())
        {
            db.Database.EnsureCreated();

            // Add columns introduced after initial schema
            try
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE DownloadedMedia ADD COLUMN LastPlaybackPosition REAL NOT NULL DEFAULT 0");
            }
            catch { /* Column already exists */ }
        }

        // Initialize playback sync serviceRembo
        app.Services.GetRequiredService<PlaybackSyncService>();

        System.Diagnostics.Debug.WriteLine("K7 MAUI - CreateMauiApp returning");
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
                options.AddEphemeralEncryptionKey()
                       .AddEphemeralSigningKey();
#else
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();
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
        services.AddSingleton<IMauiInitializeService>(static provider => new MauiHostedServiceAdapter(
            provider.GetServices<IHostedService>().OfType<OpenIddictClientSystemIntegrationActivationHandler>().Single()));

        services.AddSingleton<IMauiInitializeService>(static provider => new MauiHostedServiceAdapter(
            provider.GetServices<IHostedService>().OfType<OpenIddictClientSystemIntegrationHttpListener>().Single()));

        services.AddSingleton<IMauiInitializeService>(static provider => new MauiHostedServiceAdapter(
            provider.GetServices<IHostedService>().OfType<OpenIddictClientSystemIntegrationPipeListener>().Single()));
#endif

        services.AddScoped<IMauiInitializeScopedService, MauiDatabaseInitializer>();
    }

    internal static OpenIddictClientRegistration CreateK7Registration(string serverUrl)
    {
        return new OpenIddictClientRegistration
        {
            Issuer = new Uri(serverUrl, UriKind.Absolute),
            ProviderName = "K7",
            RegistrationId = $"K7:{serverUrl}",
            ClientId = "k7-native",
#if ANDROID || IOS || MACCATALYST
            RedirectUri = new Uri("k7://callback/login", UriKind.Absolute),
#else
            RedirectUri = new Uri("http://localhost/", UriKind.Absolute),
#endif
            Scopes = { Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, "api" }
        };
    }

    static partial void ConfigurePlatformServices(this IServiceCollection services);
}
