using K7.Server.Application;
using K7.Tests.Helpers.Smoke;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Web.SmokeTests;

[TestFixture]
public class WebHostSmokeTests
{
    private SmokeWebApplicationFactory _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new SmokeWebApplicationFactory();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public void Host_ShouldBuildServiceProvider()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.Should().NotBeNull();
    }

    [Test]
    public async Task HealthEndpoint_ShouldReturnSuccess()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/health");

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Test]
    public void MediatR_ShouldResolveAllHandlers()
    {
        MediatRHandlerResolution.ResolveAllHandlers(_factory.Services, typeof(DependencyInjection).Assembly);
    }
}
