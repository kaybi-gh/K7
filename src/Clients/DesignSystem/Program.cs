using K7.Clients.DesignSystem.Mocks;
using K7.Clients.DesignSystem.Services;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, MockAuthStateProvider>();

builder.Services.AddLocalization(opt => opt.ResourcesPath = "Resources");

// UI state: per Blazor Server circuit
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<SidebarService>();
builder.Services.AddScoped<BackButtonService>();

// Dialog and snackbar: must be Scoped (per-circuit) in Blazor Server
builder.Services.AddScoped<K7DialogService>();
builder.Services.AddScoped<IK7DialogService>(sp => sp.GetRequiredService<K7DialogService>());
builder.Services.AddScoped<K7SnackbarService>();
builder.Services.AddScoped<IK7Snackbar>(sp => sp.GetRequiredService<K7SnackbarService>());
builder.Services.AddScoped<ISpatialNavService, SpatialNavService>();
builder.Services.AddScoped<ISoftKeyboardService, NoOpSoftKeyboardService>();
builder.Services.AddScoped<SoftKeyboardJsBridge>();
builder.Services.AddScoped<IWindowsStreamFetchJsBridge, NoOpWindowsStreamFetchJsBridge>();

// Client-side mock services with mutable playback/UI state
builder.Services.AddScoped<MockAudioPlayerService>();
builder.Services.AddScoped<IAudioPlayerService>(sp => sp.GetRequiredService<MockAudioPlayerService>());
builder.Services.AddScoped<IAmbientThemeService, AmbientThemeService>();
builder.Services.AddScoped<IMusicRadioPlaybackService, MusicRadioPlaybackService>();
builder.Services.AddScoped<DemoPlayerService>();
builder.Services.AddScoped<IPlayerService>(sp => sp.GetRequiredService<DemoPlayerService>());
builder.Services.AddSingleton<IMediaPlayerService, MockMediaPlayerService>();
builder.Services.AddSingleton<IFeatureAccessService, MockFeatureAccessService>();
builder.Services.AddSingleton<ICustomAuthenticationStateProvider, MockCustomAuthStateProvider>();
builder.Services.AddSingleton<WebViewJsBridge>();
builder.Services.AddSingleton<IDeviceService, MockDeviceService>();
builder.Services.AddSingleton<IAppExitService, MockAppExitService>();
builder.Services.AddSingleton<IDeviceStorageService, MockDeviceStorageService>();
builder.Services.AddSingleton<IPageFilterStorage, MockPageFilterStorage>();
builder.Services.AddSingleton<IVolumeService, MockVolumeService>();
builder.Services.AddSingleton<IBrightnessService, MockBrightnessService>();
builder.Services.AddSingleton<IStreamUriService, MockStreamUriService>();
builder.Services.AddSingleton<ILocalUserService, MockLocalUserService>();
builder.Services.AddSingleton<ISyncPlayService, MockSyncPlayService>();
builder.Services.AddSingleton<ISleepTimerService, MockSleepTimerService>();
builder.Services.AddSingleton<K7HubClient>();

// Server API mock services
builder.Services.AddSingleton<IK7ServerService, MockK7ServerService>();
builder.Services.AddSingleton<IMediaService, MockMediaService>();
builder.Services.AddSingleton<ILibraryService, MockLibraryService>();
builder.Services.AddSingleton<IPlaylistService, MockPlaylistService>();
builder.Services.AddSingleton<ICollectionService, MockCollectionService>();
builder.Services.AddSingleton<ISearchService, MockSearchService>();
builder.Services.AddSingleton<IStreamingService, MockStreamingService>();
builder.Services.AddSingleton<IDeviceApiService, MockDeviceApiService>();
builder.Services.AddSingleton<IUserAdminService, MockUserAdminService>();
builder.Services.AddSingleton<IRatingService, MockRatingService>();
builder.Services.AddSingleton<IReviewService, MockReviewService>();
builder.Services.AddSingleton<ISocialUserService, MockSocialUserService>();
builder.Services.AddSingleton<IBackgroundTaskService, MockBackgroundTaskService>();
builder.Services.AddSingleton<IDiagnosticsService, MockDiagnosticsService>();
builder.Services.AddSingleton<IServerInfoService, MockServerInfoService>();
builder.Services.AddSingleton<IUserPreferencesService, MockUserPreferencesService>();
builder.Services.AddSingleton<IServerPreferencesService, MockServerPreferencesService>();
builder.Services.AddSingleton<IMusicIntelligenceClientService, MockMusicIntelligenceClientService>();

builder.Services.AddSingleton<IConnectivityService, MockConnectivityService>();
builder.Services.AddSingleton<ISharedProfileService, MockSharedProfileService>();
builder.Services.AddSingleton<ISharedProfileSessionService, MockSharedProfileSessionService>();
builder.Services.AddSingleton<ISharedProfileLocalCache, MockSharedProfileLocalCache>();
builder.Services.AddSingleton<ISharedProfileDevicePinService, MockSharedProfileDevicePinService>();
builder.Services.AddSingleton<IPlaybackJournal, MockPlaybackJournal>();
builder.Services.AddSingleton<ICastService, MockCastService>();
builder.Services.AddSingleton<ICastOrchestrationService, MockCastOrchestrationService>();
builder.Services.AddSingleton<IRemoteControlService, MockRemoteControlService>();
builder.Services.AddSingleton<IDownloadManager, MockDownloadManager>();

// Concrete services whose dependencies are satisfied by the mocks above
builder.Services.AddSingleton<MediaCacheStore>();
builder.Services.AddSingleton<IHomeNavigationState, HomeNavigationState>();
builder.Services.AddSingleton<IHomeFeedStore, HomeFeedStore>();
builder.Services.AddSingleton<ITvHubHostService, TvHubHostService>();
builder.Services.AddSingleton<IMediaBrowseHubCoordinator, MediaBrowseHubCoordinator>();
builder.Services.AddSingleton<IExploreGroupStore, ExploreGroupStore>();
builder.Services.AddSingleton<ILibraryGroupContextStore, LibraryGroupContextStore>();
builder.Services.AddScoped<PlaybackProgressTracker>();
builder.Services.AddScoped<AudioPlaybackProgressTracker>();
builder.Services.AddScoped<ISyncPlayMediaLoader, SyncPlayMediaLoader>();
builder.Services.AddScoped<SyncPlayPlaybackHandler>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

// Proxy endpoint: lets the browser load demo video from localhost instead of an external CDN
// that may block direct browser requests (referer checks, firewall, etc.).
app.MapGet("/demo/video", async (HttpContext ctx, IHttpClientFactory factory) =>
{
    const string sourceUrl = "https://www.w3schools.com/html/mov_bbb.mp4";
    using var client = factory.CreateClient();

    using var req = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
    if (ctx.Request.Headers.TryGetValue("Range", out var range))
        req.Headers.TryAddWithoutValidation("Range", range.ToString());

    using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ctx.RequestAborted);
    ctx.Response.StatusCode = (int)res.StatusCode;

    if (!res.IsSuccessStatusCode)
        return;

    ctx.Response.ContentType = res.Content.Headers.ContentType?.MediaType ?? "video/mp4";

    if (res.Content.Headers.ContentLength is long len)
        ctx.Response.ContentLength = len;
    if (res.Headers.TryGetValues("Accept-Ranges", out var ar))
        ctx.Response.Headers["Accept-Ranges"] = ar.First();
    if (res.Headers.TryGetValues("Content-Range", out var cr))
        ctx.Response.Headers["Content-Range"] = cr.First();

    await res.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
});

app.MapRazorComponents<K7.Clients.DesignSystem.App>()
   .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
