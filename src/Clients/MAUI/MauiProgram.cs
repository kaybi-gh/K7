using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services;
using K7.Clients.MAUI.Services.Authentication;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using CommunityToolkit.Maui;
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
            .UseMauiCommunityToolkitMediaElement()
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
            handlers.AddHandler<BlazorWebView, TransparentBlazorWebViewHandler>();
#endif
        });

        builder.Services.AddMudServices();
        builder.Services.ConfigurePlatformServices();

        builder.Services.AddSingleton<SidebarService>();
        builder.Services.AddSingleton<ThemeService>();

        builder.Services.AddHttpClient(nameof(K7ServerService));
        builder.Services.AddSingleton<IK7ServerService, K7ServerService>();
        builder.Services.AddSingleton<K7ServerManagerService>();

        builder.Services.AddSingleton<IDeviceService, DeviceService>();
        builder.Services.AddSingleton<IStreamUriService, StreamUriService>();
        builder.Services.AddSingleton<IPlayerService, PlayerService>();
        builder.Services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
        builder.Services.AddSingleton<IMediaPlayerService, MediaPlayerService>();
        builder.Services.AddSingleton<IMediaStreamSession, MediaSessionService>();
        builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();
        builder.Services.AddSingleton<K7HubClient>();
        builder.Services.AddSingleton<PlaybackProgressTracker>();
        builder.Services.AddSingleton<AudioPlaybackProgressTracker>();

        var serverUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);
        ConfigureOpenIddict(builder.Services, serverUrl);

        builder.Services.AddAuthorizationCore();
        builder.Services.AddSingleton<ICustomAuthenticationStateProvider, CustomAuthenticationStateProvider>();
        builder.Services.AddSingleton(sp => (AuthenticationStateProvider)sp.GetRequiredService<ICustomAuthenticationStateProvider>());
        builder.Services.AddSingleton<IFeatureAccessService, FeatureAccessService>();

        var app = builder.Build();

        app.Services.GetRequiredService<AudioPlaybackProgressTracker>();

        var k7ServerManagerService = app.Services.GetRequiredService<K7ServerManagerService>();
        k7ServerManagerService.BaseAddressUpdated += (sender, baseAddress) => K7ServerManagerService_BaseAddressUpdated(sender, baseAddress, app.Services);

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

        System.Diagnostics.Debug.WriteLine("K7 MAUI - Registering DbContext");
        services.AddDbContext<DbContext>(options =>
        {
            options.UseSqlite($"Filename={dbPath}");
            options.UseOpenIddict();
        });

        System.Diagnostics.Debug.WriteLine("K7 MAUI - Registering OpenIddict");
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<DbContext>();
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

                if (!string.IsNullOrEmpty(serverUrl))
                {
                    options.AddRegistration(new OpenIddictClientRegistration
                    {
                        Issuer = new Uri(serverUrl, UriKind.Absolute),
                        ProviderName = "K7",
                        ClientId = "k7-native",
#if ANDROID || IOS || MACCATALYST
                        RedirectUri = new Uri("k7://login-callback", UriKind.Absolute),
#else
                        RedirectUri = new Uri("http://localhost/", UriKind.Absolute),
#endif
                        PostLogoutRedirectUri = new Uri("http://localhost/", UriKind.Absolute),
                        Scopes = { Scopes.Email, Scopes.Profile, Scopes.OfflineAccess, "api" }
                    });
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
        services.AddSingleton<IMauiInitializeService>(static provider => new MauiHostedServiceAdapter(
            ActivatorUtilities.CreateInstance<OpenIddictClientSystemIntegrationActivationHandler>(provider)));

        services.AddSingleton<IMauiInitializeService>(static provider => new MauiHostedServiceAdapter(
            ActivatorUtilities.CreateInstance<OpenIddictClientSystemIntegrationHttpListener>(provider)));

        services.AddSingleton<IMauiInitializeService>(static provider => new MauiHostedServiceAdapter(
            ActivatorUtilities.CreateInstance<OpenIddictClientSystemIntegrationPipeListener>(provider)));
#endif

        services.AddScoped<IMauiInitializeScopedService, MauiDatabaseInitializer>();
    }

    private static async void K7ServerManagerService_BaseAddressUpdated(object? sender, string baseAddress, IServiceProvider services)
    {
        await DeviceInitializer.InitializeDeviceAsync(services);
    }

    static partial void ConfigurePlatformServices(this IServiceCollection services);
}
