using MudBlazor.Services;
using MediaClient.Web.Components;
using MediaClient.Shared.Services;
using MediaClient.Shared.Pages.Utils;
using MediaClient.Shared.Components.Utils;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
builder.Services.AddServerServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MediaClient.Web.Client._Imports).Assembly)
    .AddAdditionalAssemblies(typeof(ISharedComponentsPointer).Assembly)
    .AddAdditionalAssemblies(typeof(ISharedPagesPointer).Assembly);

app.Run();
