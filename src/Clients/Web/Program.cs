using Blazored.LocalStorage;
using System.Globalization;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.Services.K7Server;
using K7.Clients.Web.Services;
using K7.Shared.Interfaces;
using K7.Shared.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddSingleton<UnauthorizedRedirectGate>();
builder.Services.AddTransient<UnauthorizedRedirectHandler>();
builder.Services.AddHttpClient(nameof(K7ServerService), httpClient =>
{
    httpClient.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
})
.AddHttpMessageHandler<UnauthorizedRedirectHandler>();

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
builder.Services.AddSingleton<IServerPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddSingleton<INotificationAdminService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddSingleton<IFederationService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddSingleton<IApiKeyAdminService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddSingleton<IMusicIntelligenceAdminService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddSingleton<IMusicIntelligenceClientService>(sp => sp.GetRequiredService<K7ServerService>());

builder.Services.AddSingleton<ISharedProfileApi>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddSingleton<ISharedProfileLocalCache, SharedProfileLocalCache>();
builder.Services.AddSingleton<ISharedProfileService, SharedProfileService>();
builder.Services.AddSingleton<ISharedProfileSessionService, SharedProfileSessionService>();
builder.Services.AddSingleton<ISharedProfileDevicePinService, SharedProfileDevicePinService>();

builder.Services.AddSingleton<SidebarService>();
builder.Services.AddSingleton<BackButtonService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<IDeviceService, DeviceService>();
builder.Services.AddSingleton<IBrightnessService, BrightnessService>();
builder.Services.AddSingleton<IVolumeService, VolumeService>();
builder.Services.AddSingleton<ICustomAuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddSingleton<IStreamUriService, StreamUriService>();
builder.Services.AddScoped<IFeatureAccessService, FeatureAccessService>();
builder.Services.AddSingleton<IPlayerService, PlayerService>();
builder.Services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
builder.Services.AddSingleton<ISleepTimerService, SleepTimerService>();
builder.Services.AddSingleton<AutoplayService>();
builder.Services.AddSingleton<IMusicRadioPlaybackService, MusicRadioPlaybackService>();
builder.Services.AddSingleton<IMediaPlayerService, MediaPlayerService>();
builder.Services.AddSingleton<PlaybackProgressTracker>();
builder.Services.AddSingleton<AudioPlaybackProgressTracker>();
builder.Services.AddSingleton<K7HubClient>();
builder.Services.AddSingleton<MediaCacheStore>();
builder.Services.AddSingleton<IHomeNavigationState, HomeNavigationState>();
builder.Services.AddSingleton<IHomeFeedStore, HomeFeedStore>();
builder.Services.AddSingleton<ITvHubHostService, TvHubHostService>();
builder.Services.AddSingleton<IMediaBrowseHubCoordinator, MediaBrowseHubCoordinator>();
builder.Services.AddSingleton<IExploreGroupStore, ExploreGroupStore>();
builder.Services.AddSingleton<ILibraryGroupContextStore, LibraryGroupContextStore>();
builder.Services.AddSingleton<IPageFilterStorage, PageFilterStorage>();
builder.Services.AddBlazoredLocalStorageAsSingleton();
builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();
builder.Services.AddSingleton<ILocalUserService, StubbedLocalUserService>();
builder.Services.AddSingleton<IDownloadManager, NoOpDownloadManager>();
builder.Services.AddSingleton<IOfflineMediaStore, NoOpOfflineMediaStore>();
builder.Services.AddSingleton<IConnectivityService, NoOpConnectivityService>();
builder.Services.AddSingleton<IPlaybackJournal, NoOpPlaybackJournal>();
builder.Services.AddSingleton<IMusicCacheService, NoOpMusicCacheService>();
builder.Services.AddSingleton<IServerConnectionService, NoOpServerConnectionService>();
builder.Services.AddSingleton<ICastService, WebCastService>();
builder.Services.AddSingleton<ICastOrchestrationService, CastOrchestrationService>();
builder.Services.AddSingleton<RemotePlaybackHandler>();
builder.Services.AddSingleton<RemoteControlService>();
builder.Services.AddSingleton<IRemoteControlService>(sp => sp.GetRequiredService<RemoteControlService>());
builder.Services.AddSingleton<SyncPlayService>();
builder.Services.AddSingleton<ISyncPlayService>(sp => sp.GetRequiredService<SyncPlayService>());
builder.Services.AddSingleton<ISyncPlayMediaLoader, SyncPlayMediaLoader>();
builder.Services.AddSingleton<SyncPlayPlaybackHandler>();

builder.Services.AddSingleton<K7DialogService>();
builder.Services.AddSingleton<IK7DialogService>(sp => sp.GetRequiredService<K7DialogService>());
builder.Services.AddSingleton<K7SnackbarService>();
builder.Services.AddSingleton<IK7Snackbar>(sp => sp.GetRequiredService<K7SnackbarService>());
builder.Services.AddSingleton<IClientErrorReporter, ClientErrorReporter>();
builder.Services.AddSingleton<ISpatialNavService, SpatialNavService>();

var wasmHost = builder.Build();

var js = wasmHost.Services.GetRequiredService<IJSRuntime>();
CultureInfo culture;
try
{
    var cultureName = await js.InvokeAsync<string?>("blazorCulture.get");
    culture = CultureInfo.GetCultureInfo(cultureName ?? "en");
}
catch
{
    culture = CultureInfo.GetCultureInfo("en");
}

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Eagerly resolve so it starts listening to audio player events
wasmHost.Services.GetRequiredService<AudioPlaybackProgressTracker>();
wasmHost.Services.GetRequiredService<RemotePlaybackHandler>();

await wasmHost.RunAsync();

