using K7.Clients.Shared.UI.Pages;

namespace K7.Clients.ComponentTests.Pages;

[TestFixture]
public class AuthCompleteTests
{
    [Test]
    public void ResolveLaunchUri_ShouldAcceptNativeCallbackOnly()
    {
        AuthComplete.ResolveLaunchUri("k7://callback/login?code=abc")
            .Should().Be("k7://callback/login?code=abc");
        AuthComplete.ResolveLaunchUri("k7://callback/logout").Should().Be("k7://callback/logout");
        AuthComplete.ResolveLaunchUri("k7://evil/login?code=abc").Should().BeNull();
        AuthComplete.ResolveLaunchUri("https://evil.example/login").Should().BeNull();
        AuthComplete.ResolveLaunchUri(null).Should().BeNull();
    }
}
