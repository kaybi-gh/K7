using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class CachedGuestAccessTests
{
    [Test]
    public void TryGetEnabled_ShouldBeNull_WhenStorageEmpty()
    {
        var storage = Substitute.For<IDeviceStorageService>();
        storage.Get(PreferenceKeys.SERVER_INFO).Returns((string?)null);

        CachedGuestAccess.TryGetEnabled(storage).Should().BeNull();
    }

    [Test]
    public void TryGetEnabled_ShouldBeFalse_WhenCachedGuestDisabled()
    {
        var storage = Substitute.For<IDeviceStorageService>();
        storage.Get(PreferenceKeys.SERVER_INFO).Returns("""{"guestEnabled":false}""");

        CachedGuestAccess.TryGetEnabled(storage).Should().BeFalse();
    }

    [Test]
    public void TryGetEnabled_ShouldBeTrue_WhenCachedGuestEnabled()
    {
        var storage = Substitute.For<IDeviceStorageService>();
        storage.Get(PreferenceKeys.SERVER_INFO).Returns("""{"GuestEnabled":true}""");

        CachedGuestAccess.TryGetEnabled(storage).Should().BeTrue();
    }
}
