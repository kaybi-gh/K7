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

    [Test]
    public void ShouldRestoreHeadless_ShouldBeTrue_WhenSoloUnlocked()
    {
        var users = SoloUsers("solo-1", unlocked: true);

        MauiSessionRestore.ShouldRestoreHeadless(users, serverConfigured: true).Should().BeTrue();
    }

    [Test]
    public void ShouldRestoreHeadless_ShouldBeTrue_WhenSoloPinUserIsUnlocked()
    {
        var users = SoloUsers("solo-1", unlocked: true, hasPin: true);

        MauiSessionRestore.ShouldRestoreHeadless(users, serverConfigured: true).Should().BeTrue();
    }

    [Test]
    public void ShouldRestoreHeadless_ShouldBeTrue_WhenLastUserHasNoPin()
    {
        var users = SoloUsers("user-1", unlocked: false, singleUser: false, hasPin: false);

        MauiSessionRestore.ShouldRestoreHeadless(users, serverConfigured: true).Should().BeTrue();
    }

    [Test]
    public void ShouldRestoreHeadless_ShouldBeFalse_WhenLastUserHasPinAndIsLocked()
    {
        var users = SoloUsers("user-1", unlocked: false, singleUser: false, hasPin: true);

        MauiSessionRestore.ShouldRestoreHeadless(users, serverConfigured: true).Should().BeFalse();
    }

    [Test]
    public void ShouldRestoreHeadless_ShouldBeFalse_WhenServerIsNotConfigured()
    {
        var users = SoloUsers("solo-1", unlocked: true);

        MauiSessionRestore.ShouldRestoreHeadless(users, serverConfigured: false).Should().BeFalse();
    }

    [Test]
    public void ShouldRestoreHeadless_ShouldBeFalse_WhenNoLastActive()
    {
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([]);
        users.GetLastActive().Returns((LocalUser?)null);
        users.IsSingleUserMode.Returns(false);

        MauiSessionRestore.ShouldRestoreHeadless(users, serverConfigured: true).Should().BeFalse();
    }

    [Test]
    public void CanReuseLastRefreshToken_ShouldBeFalse_WhenTokenIsMissing()
    {
        var users = Substitute.For<ILocalUserService>();
        var last = new LocalUser
        {
            IdentityUserId = "user-1",
            UserName = "kay",
            RefreshToken = ""
        };

        MauiSessionRestore.CanReuseLastRefreshToken(last, users).Should().BeFalse();
        MauiSessionRestore.CanReuseLastRefreshToken(null, users).Should().BeFalse();
    }

    [Test]
    public void CanReuseLastRefreshToken_ShouldBeTrue_WhenUserHasNoPin()
    {
        var last = new LocalUser
        {
            IdentityUserId = "user-1",
            UserName = "kay",
            RefreshToken = "rt",
            HasPin = false
        };
        var users = Substitute.For<ILocalUserService>();
        users.IsSingleUserUnlocked("user-1").Returns(false);

        MauiSessionRestore.CanReuseLastRefreshToken(last, users).Should().BeTrue();
    }

    [Test]
    public void CanReuseLastRefreshToken_ShouldBeFalse_WhenPinUserIsLocked()
    {
        var last = new LocalUser
        {
            IdentityUserId = "user-1",
            UserName = "kay",
            RefreshToken = "rt",
            HasPin = true
        };
        var users = Substitute.For<ILocalUserService>();
        users.IsSingleUserUnlocked("user-1").Returns(false);

        MauiSessionRestore.CanReuseLastRefreshToken(last, users).Should().BeFalse();
    }

    [Test]
    public void CanReuseLastRefreshToken_ShouldBeTrue_WhenPinUserIsUnlocked()
    {
        var last = new LocalUser
        {
            IdentityUserId = "solo-1",
            UserName = "kay",
            RefreshToken = "rt",
            HasPin = true
        };
        var users = Substitute.For<ILocalUserService>();
        users.IsSingleUserUnlocked("solo-1").Returns(true);

        MauiSessionRestore.CanReuseLastRefreshToken(last, users).Should().BeTrue();
    }

    [Test]
    public void ShouldClearStoredTokens_ShouldBeTrue_WhenInvalidGrant()
    {
        MauiSessionRestore.ShouldClearStoredTokens(invalidGrant: true).Should().BeTrue();
    }

    [Test]
    public void ShouldClearStoredTokens_ShouldBeFalse_WhenTransientFailure()
    {
        MauiSessionRestore.ShouldClearStoredTokens(invalidGrant: false).Should().BeFalse();
    }

    [Test]
    public void ShouldRetryHeadlessRestore_ShouldBeTrue_WhenPreviousRestoreLeftNoOnlineToken()
    {
        MauiSessionRestore.ShouldRetryHeadlessRestore(
            serverConfigured: true,
            restoreAlreadyAttempted: true,
            restoreInFlight: false,
            hasUsableOnlineAccessToken: false).Should().BeTrue();
    }

    [Test]
    public void ShouldRetryHeadlessRestore_ShouldBeFalse_WhenServerIsNotConfigured()
    {
        MauiSessionRestore.ShouldRetryHeadlessRestore(
            serverConfigured: false,
            restoreAlreadyAttempted: true,
            restoreInFlight: false,
            hasUsableOnlineAccessToken: false).Should().BeFalse();
    }

    [Test]
    public void ShouldRetryHeadlessRestore_ShouldBeFalse_WhenRestoreNeverStarted()
    {
        MauiSessionRestore.ShouldRetryHeadlessRestore(
            serverConfigured: true,
            restoreAlreadyAttempted: false,
            restoreInFlight: false,
            hasUsableOnlineAccessToken: false).Should().BeFalse();
    }

    [Test]
    public void ShouldRetryHeadlessRestore_ShouldBeFalse_WhenRestoreIsInFlight()
    {
        MauiSessionRestore.ShouldRetryHeadlessRestore(
            serverConfigured: true,
            restoreAlreadyAttempted: true,
            restoreInFlight: true,
            hasUsableOnlineAccessToken: false).Should().BeFalse();
    }

    [Test]
    public void ShouldRetryHeadlessRestore_ShouldBeFalse_WhenOnlineTokenIsUsable()
    {
        MauiSessionRestore.ShouldRetryHeadlessRestore(
            serverConfigured: true,
            restoreAlreadyAttempted: true,
            restoreInFlight: false,
            hasUsableOnlineAccessToken: true).Should().BeFalse();
    }

    private static ILocalUserService SoloUsers(
        string id,
        bool unlocked,
        bool singleUser = true,
        bool hasPin = false)
    {
        var user = new LocalUser
        {
            IdentityUserId = id,
            UserName = "kay",
            RefreshToken = "rt",
            HasPin = hasPin
        };
        var users = Substitute.For<ILocalUserService>();
        users.GetAll().Returns([user]);
        users.GetLastActive().Returns(user);
        users.IsSingleUserMode.Returns(singleUser);
        users.IsSingleUserUnlocked(id).Returns(unlocked);
        return users;
    }
}
