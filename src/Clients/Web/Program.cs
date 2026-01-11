using Blazored.LocalStorage;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.Services.K7Server;
using K7.Clients.Web.Services;
using K7.Shared.Interfaces;
using K7.Shared.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

builder.Services.AddMudServices();

builder.Services.AddHttpClient<IK7ServerService, K7ServerService>(httpClient =>
{
    httpClient.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});

builder.Services.AddSingleton<SidebarService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<IDeviceService, DeviceService>();
builder.Services.AddSingleton<ICustomAuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddSingleton<IPlayerService, PlayerService>();
builder.Services.AddBlazoredLocalStorageAsSingleton();
builder.Services.AddSingleton<IDeviceStorageService, DeviceStorageService>();

builder.Services.AddHttpClient("BackendAPI", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress + "/bff");
})
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

var wasmHost = builder.Build();

await DeviceInitializer.InitializeDeviceAsync(wasmHost.Services);

await wasmHost.RunAsync();

