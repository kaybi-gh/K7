using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace K7.Tests.Helpers.Smoke;

public sealed class SmokeWebApplicationFactory : WebApplicationFactory<Program>
{
    private SmokePathLayout? _paths;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _paths = new SmokePathLayout();

        builder.UseEnvironment(Environments.Development);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:Name"] = _paths.DatabaseName,
                ["Paths:Config"] = _paths.Config,
                ["Paths:Metadatas"] = _paths.Metadatas,
                ["Paths:Logs"] = _paths.Logs,
                ["Paths:Transcoding"] = _paths.Transcoding,
                ["Paths:FFMpegBinaryFolder"] = "",
                ["Security:ForceHttps"] = "false",
                ["Security:ApiKeys:HashSecret"] = "smoke-test-api-key-hash-secret",
                ["Authentication:Local:SignInEnabled"] = "true",
                ["Authentication:Oidc:Enabled"] = "false",
                ["SmokeTest:SkipFfmpegVerification"] = "true"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var user = Substitute.For<IUser>();
            user.Id.Returns(Guid.NewGuid());
            user.IdentityId.Returns("smoke-user");

            services.RemoveAll<IUser>();
            services.AddTransient(_ => user);
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();
            _paths?.Dispose();
            _paths = null;
            return;
        }

        base.Dispose(disposing);
    }
}
