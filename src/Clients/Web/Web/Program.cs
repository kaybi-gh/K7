using MudBlazor.Services;
using K7.Clients.Web.Components;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.Pages.Utils;
using K7.Clients.Shared.Components.Utils;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(); // TODO - Well placed?

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddConfigurations(builder.Configuration);
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

app.MapDefaultEndpoints(); // TODO - Well placed?
app.UseStaticFiles(); // TODO - Use new static files
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(K7.Clients.Web.Client._Imports).Assembly)
    .AddAdditionalAssemblies(typeof(ISharedComponentsPointer).Assembly)
    .AddAdditionalAssemblies(typeof(ISharedPagesPointer).Assembly);

app.Run();
