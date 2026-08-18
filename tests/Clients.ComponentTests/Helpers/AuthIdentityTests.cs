using System.Security.Claims;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class AuthIdentityTests
{
    [Test]
    public void GetUserId_ShouldReadNameIdentifier_WhenPresent()
    {
        var user = Principal("Bearer", new Claim(ClaimTypes.NameIdentifier, "user-a"));
        AuthIdentity.GetUserId(user).Should().Be("user-a");
    }

    [Test]
    public void GetUserId_ShouldReadSub_WhenNameIdentifierIsMissing()
    {
        var user = Principal(AuthIdentity.OfflineAuthenticationType, new Claim("sub", "user-b"));
        AuthIdentity.GetUserId(user).Should().Be("user-b");
    }

    [Test]
    public void IsOnlineAuthenticated_ShouldBeFalse_WhenOfflineSession()
    {
        var user = Principal(AuthIdentity.OfflineAuthenticationType, new Claim("sub", "user-b"));
        AuthIdentity.IsOnlineAuthenticated(user).Should().BeFalse();
    }

    [Test]
    public void IsOnlineAuthenticated_ShouldBeTrue_WhenBearerSessionHasUserId()
    {
        var user = Principal("Bearer", new Claim(ClaimTypes.NameIdentifier, "user-a"));
        AuthIdentity.IsOnlineAuthenticated(user).Should().BeTrue();
    }

    [Test]
    public void IsOnlineAuthenticated_ShouldBeFalse_WhenAnonymous()
    {
        AuthIdentity.IsOnlineAuthenticated(new ClaimsPrincipal(new ClaimsIdentity())).Should().BeFalse();
    }

    private static ClaimsPrincipal Principal(string authenticationType, params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType));
}
