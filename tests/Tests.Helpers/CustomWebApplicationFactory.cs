using System.Data.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Tests.Helpers.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace K7.Tests.Helpers;

using static DatabaseFixture;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _connection;

    public CustomWebApplicationFactory(DbConnection connection)
    {
        _connection = connection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SmokeTest:SkipFfmpegVerification"] = "true"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            var userSubstitute = Substitute.For<IUser>();
            userSubstitute.IdentityId.Returns(x => GetUserId());

            services
                .RemoveAll<IUser>()
                .AddTransient(provider => userSubstitute);

            services
                .RemoveAll<DbContextOptions<ApplicationDbContext>>()
                .AddDbContext<IApplicationDbContext, ApplicationDbContext>((sp, options) =>
                {
                    options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                    options.UseNpgsql(_connection);
                });
        });
    }
}
