using System.Reflection;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;

namespace K7.Clients.MAUI.SmokeTests;

[TestFixture]
public class MauiHostSmokeTests
{
    [Test]
    public void CreateK7Registration_ShouldConfigureNativeClient()
    {
        var registration = MauiProgram.CreateK7Registration("https://k7.local");

        registration.ClientId.Should().Be("k7-native");
        registration.Issuer.Should().Be(new Uri("https://k7.local"));
        registration.Scopes.Should().Contain("api");
    }

    [Test]
    public void MauiProgram_ShouldExposeCreateMauiAppEntryPoint()
    {
        var method = typeof(MauiProgram).GetMethod(
            "CreateMauiApp",
            BindingFlags.Public | BindingFlags.Static);

        method.Should().NotBeNull();
    }

    [Test]
    public void MauiClientServices_ShouldLiveInMauiAssembly()
    {
        typeof(ConnectivityService).Assembly.GetName().Name.Should().Be("K7.Clients.MAUI");
        typeof(PlayerService).Assembly.GetName().Name.Should().Be("K7.Clients.MAUI");
    }
}
