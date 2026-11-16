using System.Security.Claims;
using K7.Server.Domain.Constants;
using K7.Server.Web.Infrastructure;
using K7.Server.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Web.SmokeTests;

/// <summary>
/// Stream endpoints accept a real account and a share visitor session in the same request.
/// The acting user must then be the real account: the visitor account has no restriction
/// of its own, so it must never replace a restricted user.
/// </summary>
[TestFixture]
public class CurrentUserTests
{
    private static ClaimsIdentity ShareIdentity(string id) => new(
        [new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, Roles.ShareVisitor)],
        ShareSessionDefaults.AuthenticationScheme);

    private static ClaimsIdentity AccountIdentity(string id) => new(
        [new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, Roles.Guest)],
        IdentityConstants.ApplicationScheme);

    private static CurrentUser Create(params ClaimsIdentity[] identities)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identities) }
        };
        return new CurrentUser(accessor, new ServiceCollection().BuildServiceProvider());
    }

    [Test]
    public void IdentityId_ShouldBeTheRealAccount_WhenShareSessionComesFirst()
    {
        var user = Create(ShareIdentity("visitor"), AccountIdentity("guest"));

        user.IdentityId.Should().Be("guest");
    }

    [Test]
    public void IdentityId_ShouldBeTheRealAccount_WhenShareSessionComesLast()
    {
        var user = Create(AccountIdentity("guest"), ShareIdentity("visitor"));

        user.IdentityId.Should().Be("guest");
    }

    [Test]
    public void IdentityId_ShouldBeTheVisitorAccount_WhenThereIsNoRealAccount()
    {
        var user = Create(new ClaimsIdentity(), ShareIdentity("visitor"));

        user.IdentityId.Should().Be("visitor");
    }

    [Test]
    public void IdentityId_ShouldBeNull_WhenNobodyIsAuthenticated()
    {
        var user = Create(new ClaimsIdentity());

        user.IdentityId.Should().BeNull();
    }
}
