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

builder.Services.AddHttpClient<K7ServerService>(httpClient =>
{
    httpClient.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});

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
builder.Services.AddTransient<IServerInfoService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IBackgroundTaskService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IDiagnosticsService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IUserPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());
builder.Services.AddTransient<IServerPreferencesService>(sp => sp.GetRequiredService<K7ServerService>());

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
builder.Services.AddSingleton<IMediaPlayerService, MediaPlayerService>();
builder.Services.AddSingleton<PlaybackProgressTracker>();
builder.Services.AddSingleton<AudioPlaybackProgressTracker>();
builder.Services.AddSingleton<K7HubClient>();
builder.Services.AddBlazoredLocalStorageAsSingleton();
builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();
builder.Services.AddSingleton<ILocalUserService, StubbedLocalUserService>();

builder.Services.AddSingleton<K7DialogService>();
builder.Services.AddSingleton<IK7DialogService>(sp => sp.GetRequiredService<K7DialogService>());
builder.Services.AddSingleton<K7SnackbarService>();
builder.Services.AddSingleton<IK7Snackbar>(sp => sp.GetRequiredService<K7SnackbarService>());

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

await wasmHost.RunAsync();

