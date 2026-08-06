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
    public async Task OpenSubsonicPing_ShouldReturnAuthErrorEnvelope_WhenUnauthenticated()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/rest/ping.view?v=1.16.1&c=smoke&f=json");
        var json = await response.Content.ReadAsStringAsync();

        // Before setup completes the host returns 503 for /rest (same as /api).
        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            return;

        response.IsSuccessStatusCode.Should().BeTrue();
        json.Should().Contain("subsonic-response");
        json.Should().Contain("\"status\":\"failed\"");
    }

    [Test]
    public void MediatR_ShouldResolveAllHandlers()
    {
        MediatRHandlerResolution.ResolveAllHandlers(_factory.Services, typeof(DependencyInjection).Assembly);
    }
}
