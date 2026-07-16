using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.DesignSystem.SmokeTests;

[TestFixture]
public class DesignSystemHostSmokeTests
{
    private WebApplicationFactory<Program> _factory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _factory.Dispose();
    }

    [Test]
    public void Host_ShouldResolveCoreClientServices()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<IK7ServerService>().Should().NotBeNull();
        sp.GetRequiredService<IPlayerService>().Should().NotBeNull();
        sp.GetRequiredService<IK7DialogService>().Should().NotBeNull();
        sp.GetRequiredService<IK7Snackbar>().Should().NotBeNull();
        sp.GetRequiredService<ThemeService>().Should().NotBeNull();
        sp.GetRequiredService<ISpatialNavService>().Should().NotBeNull();
    }

    [Test]
    public async Task Root_ShouldReturnHtml()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().Should().Contain("<html");
    }
}
