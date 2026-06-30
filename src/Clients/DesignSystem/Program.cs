using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.DesignSystem.Mocks;
using K7.Clients.DesignSystem.Services;
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

// Concrete services with no external dependencies
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<SidebarService>();
builder.Services.AddSingleton<BackButtonService>();

// Dialog and snackbar: must be Scoped (per-circuit) in Blazor Server
builder.Services.AddScoped<K7DialogService>();
builder.Services.AddScoped<IK7DialogService>(sp => sp.GetRequiredService<K7DialogService>());
builder.Services.AddScoped<K7SnackbarService>();
builder.Services.AddScoped<IK7Snackbar>(sp => sp.GetRequiredService<K7SnackbarService>());
builder.Services.AddScoped<ISpatialNavService, SpatialNavService>();

// Client-side mock services
builder.Services.AddSingleton<MockAudioPlayerService>();
builder.Services.AddSingleton<IAudioPlayerService>(sp => sp.GetRequiredService<MockAudioPlayerService>());
builder.Services.AddSingleton<DemoPlayerService>();
builder.Services.AddSingleton<IPlayerService>(sp => sp.GetRequiredService<DemoPlayerService>());
builder.Services.AddSingleton<IMediaPlayerService, MockMediaPlayerService>();
builder.Services.AddSingleton<IFeatureAccessService, MockFeatureAccessService>();
builder.Services.AddSingleton<ICustomAuthenticationStateProvider, MockCustomAuthStateProvider>();
builder.Services.AddSingleton<IDeviceService, MockDeviceService>();
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
builder.Services.AddSingleton<IBackgroundTaskService, MockBackgroundTaskService>();
builder.Services.AddSingleton<IDiagnosticsService, MockDiagnosticsService>();
builder.Services.AddSingleton<IServerInfoService, MockServerInfoService>();
builder.Services.AddSingleton<IUserPreferencesService, MockUserPreferencesService>();
builder.Services.AddSingleton<IServerPreferencesService, MockServerPreferencesService>();
builder.Services.AddSingleton<IMusicIntelligenceClientService, MockMusicIntelligenceClientService>();

builder.Services.AddSingleton<IConnectivityService, MockConnectivityService>();
builder.Services.AddSingleton<IPlaybackJournal, MockPlaybackJournal>();
builder.Services.AddSingleton<ICastService, MockCastService>();
builder.Services.AddSingleton<ICastOrchestrationService, MockCastOrchestrationService>();
builder.Services.AddSingleton<IRemoteControlService, MockRemoteControlService>();
builder.Services.AddSingleton<IDownloadManager, MockDownloadManager>();

// Concrete services whose dependencies are satisfied by the mocks above
builder.Services.AddSingleton<MediaCacheStore>();
builder.Services.AddSingleton<PlaybackProgressTracker>();
builder.Services.AddSingleton<AudioPlaybackProgressTracker>();
builder.Services.AddSingleton<ISyncPlayMediaLoader, SyncPlayMediaLoader>();
builder.Services.AddSingleton<SyncPlayPlaybackHandler>();
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

    var req = new HttpRequestMessage(HttpMethod.Get, sourceUrl);
    if (ctx.Request.Headers.TryGetValue("Range", out var range))
        req.Headers.TryAddWithoutValidation("Range", range.ToString());

    var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

    ctx.Response.StatusCode = (int)res.StatusCode;
    ctx.Response.ContentType = "video/mp4";

    if (res.Content.Headers.ContentLength is long len)
        ctx.Response.ContentLength = len;
    if (res.Headers.TryGetValues("Accept-Ranges", out var ar))
        ctx.Response.Headers["Accept-Ranges"] = ar.First();
    if (res.Headers.TryGetValues("Content-Range", out var cr))
        ctx.Response.Headers["Content-Range"] = cr.First();

    await res.Content.CopyToAsync(ctx.Response.Body);
});

app.MapRazorComponents<K7.Clients.DesignSystem.App>()
   .AddInteractiveServerRenderMode();

app.Run();
