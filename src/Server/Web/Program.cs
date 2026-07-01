using K7.Clients.Shared.UI.Pages.Utils;
using K7.Server.Application;
using K7.Server.Infrastructure.Database.Context;
using K7.Shared;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Server.Infrastructure.Database.Context.Oidc;
using K7.Server.Infrastructure.FileSystem;
using K7.Server.Infrastructure.MediaProcessing;
using K7.Server.Infrastructure.ExternalServices;
using K7.Server.Web;
using K7.Server.Web.Components;
using K7.Server.Web.Components.Account;
using K7.Server.Web.Endpoints.Hubs;
using K7.Server.Web.Middleware;
using K7.Server.Web.Services;
using Scalar.AspNetCore;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    if (OpenApiSetup.IsRequested)
    {
        builder.RunOpenApiGeneration();
        return;
    }

    builder.AddServiceDefaults();

    builder.Services.AddConfigurations(builder.Configuration);
    builder.Services.EnsurePathsExist();
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddMediaProcessingServices();
    builder.Services.AddExternalServices();
    builder.Services.AddWebServices(builder.Configuration);
    builder.Services.AddEndpoints();
    builder.Services.ConfigureCors(builder.Configuration);
    builder.Host.UseSerilog();
    builder.Configuration.ConfigureSerilog();

    var app = builder.Build();
    app.InitializeMediaProcessing();
    app.UseSerilogRequestLogging();
    app.MapDefaultEndpoints();

    await app.InitializeDatabaseAsync();
    await app.InitializeOidcClientsAsync();

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

    app.UseForwardedHeaders();
    app.UseHealthChecks("/health");
    app.UseHttpsRedirection();
    app.UseAuthLegacyRedirects();

    var supportedCultures = SupportedLanguages.Interface.Select(l => l.Code).ToArray();
    app.UseRequestLocalization(new RequestLocalizationOptions()
        .SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures));

    app.MapStaticAssets();

    app.UseSetupRequired();

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
    app.UseMiddleware<FederationGuardMiddleware>();

    app.UseExceptionHandler(options => { });
    app.MapEndpoints();
    app.MapAdditionalIdentityEndpoints();
    app.MapHub<K7Hub>("/hub");
    app.MapRazorComponents<App>()
        .WithStaticAssets()
        .AddInteractiveServerRenderMode()
        .AddInteractiveWebAssemblyRenderMode()
        .AddAdditionalAssemblies(
            typeof(K7.Clients.Web._Imports).Assembly,
            typeof(ISharedPagesPointer).Assembly);

    app.MapScalarApiReference(o => {
        o.WithOpenApiRoutePattern("/openapi/specification.json");
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
