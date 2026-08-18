using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MauiBlazorStartPathTests
{
    [Test]
    public void Resolve_ShouldOpenHome_WhenSoloUnlockedLastActive()
    {
        var users = ILocalUserServiceSubstitute("solo-1", unlocked: true, singleUser: true);

        MauiBlazorStartPath.Resolve(users).Should().Be(MauiBlazorStartPath.Home);
    }

    [Test]
    public void Resolve_ShouldOpenSelectProfile_WhenSoloNotUnlocked()
    {
        var users = ILocalUserServiceSubstitute("solo-1", unlocked: false, singleUser: true);

        MauiBlazorStartPath.Resolve(users).Should().Be(MauiBlazorStartPath.SelectProfile);
    }

    [Test]
    public void Resolve_ShouldOpenSelectProfile_WhenLocalUsersExist()
    {
        var users = ILocalUserServiceSubstitute("user-1", unlocked: false, singleUser: false);

        MauiBlazorStartPath.Resolve(users).Should().Be(MauiBlazorStartPath.SelectProfile);
    }

    [Test]
    public void Resolve_ShouldOpenWelcome_WhenNoLocalUsers()
    {
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([]);
        users.GetLastActive().Returns((LocalUser?)null);

        MauiBlazorStartPath.Resolve(users).Should().Be(MauiBlazorStartPath.Welcome);
    }

    [Test]
    public void Resolve_ShouldOpenLinkDevice_WhenTvAndGuestDisabled()
    {
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([]);
        users.GetLastActive().Returns((LocalUser?)null);

        MauiBlazorStartPath.Resolve(users, isTv: true, guestEnabled: false)
            .Should().Be(MauiBlazorStartPath.LinkDevice);
    }

    [Test]
    public void Resolve_ShouldOpenWelcome_WhenTvAndGuestEnabled()
    {
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([]);
        users.GetLastActive().Returns((LocalUser?)null);

        MauiBlazorStartPath.Resolve(users, isTv: true, guestEnabled: true)
            .Should().Be(MauiBlazorStartPath.Welcome);
    }

    [Test]
    public void Resolve_ShouldOpenWelcome_WhenTvAndGuestUnknown()
    {
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([]);
        users.GetLastActive().Returns((LocalUser?)null);

        MauiBlazorStartPath.Resolve(users, isTv: true, guestEnabled: null)
            .Should().Be(MauiBlazorStartPath.Welcome);
    }

    [Test]
    public void Resolve_ShouldOpenWelcome_WhenGuestDisabledOnPhone()
    {
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([]);
        users.GetLastActive().Returns((LocalUser?)null);

        MauiBlazorStartPath.Resolve(users, isTv: false, guestEnabled: false)
            .Should().Be(MauiBlazorStartPath.Welcome);
    }

    private static ILocalUserService ILocalUserServiceSubstitute(string id, bool unlocked, bool singleUser)
    {
        var user = new LocalUser
        {
            IdentityUserId = id,
            UserName = "kay",
            RefreshToken = "rt"
        };
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([user]);
        users.GetLastActive().Returns(user);
        users.IsSingleUserMode.Returns(singleUser);
        users.IsSingleUserUnlocked(id).Returns(unlocked);
        return users;
    }
}
