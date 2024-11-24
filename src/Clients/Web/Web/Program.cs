using MudBlazor.Services;
using K7.Clients.Web.Components;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.Pages.Utils;
using K7.Clients.Shared.Components.Utils;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // TODO - Well placed?

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddConfigurations(builder.Configuration);
builder.Services.AddMudServices();
builder.Services.AddServerServices();
builder.Services.AddScoped<IDeviceService, DeviceService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection(); // TODO - Do we really want to enforce https to users?
app.MapDefaultEndpoints(); // TODO - Well placed?
app.MapStaticAssets();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .WithStaticAssets()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(K7.Clients.Web.Client._Imports).Assembly,
        typeof(ISharedComponentsPointer).Assembly,
        typeof(ISharedPagesPointer).Assembly);

app.Run();
