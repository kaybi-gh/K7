using System.Data.Common;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Services;
using MediaServer.Domain.Interfaces;
using MediaServer.Infrastructure.Context.Data;
using MediaServer.Tests.Helpers.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MediaServer.Tests.Helpers;

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
        builder.ConfigureTestServices(services =>
        {
            var userSubstitute = Substitute.For<IUser>();
            userSubstitute.Id.Returns(x => GetUserId());

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
