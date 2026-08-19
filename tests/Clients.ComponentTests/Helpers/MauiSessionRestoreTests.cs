using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MauiSessionRestoreTests
{
    [Test]
    public void ShouldRestore_ShouldBeFalse_WhenServerIsNotConfigured()
    {
        var users = SoloUsers("solo-1", unlocked: true);

        MauiSessionRestore.ShouldRestore(users, serverConfigured: false).Should().BeFalse();
    }

    [Test]
    public void ShouldRestore_ShouldBeTrue_WhenSoloUnlockedAndServerConfigured()
    {
        var users = SoloUsers("solo-1", unlocked: true);

        MauiSessionRestore.ShouldRestore(users, serverConfigured: true).Should().BeTrue();
    }

    [Test]
    public void ShouldRestore_ShouldBeFalse_WhenSoloNotUnlocked()
    {
        var users = SoloUsers("solo-1", unlocked: false);

        MauiSessionRestore.ShouldRestore(users, serverConfigured: true).Should().BeFalse();
    }

    [Test]
    public void ShouldRestore_ShouldBeFalse_WhenSoloModeOff()
    {
        var users = SoloUsers("user-1", unlocked: true, singleUser: false);

        MauiSessionRestore.ShouldRestore(users, serverConfigured: true).Should().BeFalse();
    }

    [Test]
    public void ShouldRestore_ShouldBeFalse_WhenNoLastActive()
    {
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([]);
        users.GetLastActive().Returns((LocalUser?)null);
        users.IsSingleUserMode.Returns(true);

        MauiSessionRestore.ShouldRestore(users, serverConfigured: true).Should().BeFalse();
    }

    private static ILocalUserService SoloUsers(string id, bool unlocked, bool singleUser = true)
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
