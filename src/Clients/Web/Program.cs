using Blazored.LocalStorage;
using System.Globalization;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.Services.K7Server;
using K7.Clients.Web.Services;
using K7.Shared.Interfaces;
using K7.Shared.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddTransient<UnauthorizedRedirectHandler>();
builder.Services.AddHttpClient<K7ServerService>(httpClient =>
{
    httpClient.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
})
.AddHttpMessageHandler<UnauthorizedRedirectHandler>();

builder.Services.AddTransient<IK7ServerService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IMediaService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<ILibraryService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IPlaylistService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<ICollectionService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<ISearchService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IStreamingService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IDeviceApiService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IUserAdminService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IRatingService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddTransient<IReviewService>(sp => sp.GetRequiredService<K7ServerService>());
        builder.Services.AddTransient<ISocialUserService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IServerInfoService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IBackgroundTaskService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IDiagnosticsService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IUserPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IServerPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<INotificationAdminService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IFederationService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IApiKeyAdminService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IMusicIntelligenceAdminService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IMusicIntelligenceClientService>(sp => sp.GetRequiredService<K7ServerService>());

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

builder.Services.AddHttpClient("BackendAPI", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress + "/bff");
})
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

var wasmHost = builder.Build();

var js = wasmHost.Services.GetRequiredService<IJSRuntime>();
var cultureName = await js.InvokeAsync<string?>("blazorCulture.get");
var culture = new CultureInfo(cultureName ?? "en");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Eagerly resolve so it starts listening to audio player events
wasmHost.Services.GetRequiredService<AudioPlaybackProgressTracker>();
wasmHost.Services.GetRequiredService<RemotePlaybackHandler>();

await wasmHost.RunAsync();

