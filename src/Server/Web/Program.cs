using K7.Server.Application;
using K7.Server.Infrastructure.Context;
using K7.Server.Infrastructure.Context.Data;
using K7.Server.Infrastructure.FileSystem;
using K7.Server.Web;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults(); // TODO - Well placed?
    builder.Services.AddConfigurations(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices();
    builder.Services.AddWebServices();
    builder.Services.ConfigureCors();
    builder.Host.UseSerilog();
    builder.Configuration.ConfigureSerilog();

    var app = builder.Build();
    app.InitializeFileSystem();
    app.InitializeFFMpeg();
    app.UseSerilogRequestLogging();
    app.MapDefaultEndpoints(); // TODO - Well placed?

    if (args.Contains("--init-db"))
    {
        await app.InitializeDatabaseAsync();
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
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

    app.UseCors();
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
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
