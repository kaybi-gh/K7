using MediaServer.Application;
using MediaServer.Infrastructure.Context;
using MediaServer.Infrastructure.Context.Data;
using MediaServer.Infrastructure.FileSystem;
using MediaServer.Web;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddConfigurations(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices();
    builder.Services.AddWebServices();
    builder.Host.UseSerilog();
    builder.Configuration.ConfigureSerilog();

    var app = builder.Build();
    app.InitializeFileSystem();
    app.UseSerilogRequestLogging();    

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment() && args.Contains("--init"))
    {
        await app.InitializeDatabaseAsync();
    }
    else
    {
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    app.UseHealthChecks("/health");
    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseSwaggerUi(settings =>
    {
        settings.Path = "/api";
        settings.DocumentPath = "/api/specification.json";
    });

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller}/{action=Index}/{id?}");

    app.MapRazorPages();
    app.MapFallbackToFile("index.html");
    app.UseExceptionHandler(options => { });
    app.Map("/", () => Results.Redirect("/api"));
    app.MapEndpoints();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
public partial class Program { }
