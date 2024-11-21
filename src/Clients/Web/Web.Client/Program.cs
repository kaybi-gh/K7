using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Web.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddConfigurations(builder.Configuration);
builder.Services.AddClientServices();
builder.Services.AddSingleton<IFormFactorService, FormFactorService>();

await builder.Build().RunAsync();
