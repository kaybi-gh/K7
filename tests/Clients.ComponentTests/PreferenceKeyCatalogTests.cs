using FluentAssertions;
using K7.Shared;

namespace K7.Clients.ComponentTests;

[TestFixture]
public class PreferenceKeyCatalogTests
{
    [Test]
    public void CustomizationKeyNames_ShouldExcludeIdentityKeys()
    {
        var keys = PreferenceKeyCatalog.CustomizationKeyNames.ToList();

        keys.Should().NotContain(PreferenceKeys.K7_SERVER_URL.Name);
        keys.Should().NotContain(PreferenceKeys.DEVICE_ID.Name);
        keys.Should().NotContain(PreferenceKeys.ACCESS_TOKEN.Name);
        keys.Should().NotContain(PreferenceKeys.REFRESH_TOKEN.Name);
        keys.Should().NotContain(PreferenceKeys.LOCAL_USERS.Name);
        keys.Should().NotContain(PreferenceKeys.SERVER_INFO.Name);
        keys.Should().NotContain(PreferenceKeys.LAST_ACTIVE_USER_ID.Name);
    }

    [Test]
    public void CustomizationKeyNames_ShouldIncludeDeviceCustomizationKeys()
    {
        var keys = PreferenceKeyCatalog.CustomizationKeyNames.ToList();

        keys.Should().Contain(PreferenceKeys.PLAYER_VOLUME.Name);
        keys.Should().Contain(PreferenceKeys.PAGE_SIDEBAR_COLLAPSED.Name);
        keys.Should().Contain(PreferenceKeys.THEME_SONGS_DISABLED_ON_DEVICE.Name);
        keys.Should().Contain(PreferenceKeys.MAX_DOWNLOAD_STORAGE_BYTES.Name);
        keys.Should().Contain(PreferenceKeys.PINNED_SHARED_PROFILE_IDS.Name);
    }

    [Test]
    public void RestorePreservedStringValues_ShouldRewriteSnapshotAfterClear()
    {
        var store = new Dictionary<string, string>
        {
            [PreferenceKeys.K7_SERVER_URL.Name] = "https://k7.example.com",
            [PreferenceKeys.DEVICE_ID.Name] = "device-1",
            [PreferenceKeys.PLAYER_VOLUME.Name] = "0.5",
        };

        var snapshot = PreferenceKeyCatalog.SnapshotPreservedStringValues(name =>
            store.TryGetValue(name, out var value) ? value : null);

        store.Remove(PreferenceKeys.K7_SERVER_URL.Name);
        store.Remove(PreferenceKeys.DEVICE_ID.Name);
        store.Remove(PreferenceKeys.PLAYER_VOLUME.Name);

        PreferenceKeyCatalog.RestorePreservedStringValues(snapshot, (name, value) => store[name] = value);

        store.Should().ContainKey(PreferenceKeys.K7_SERVER_URL.Name)
            .WhoseValue.Should().Be("https://k7.example.com");
        store.Should().ContainKey(PreferenceKeys.DEVICE_ID.Name)
            .WhoseValue.Should().Be("device-1");
        store.Should().NotContainKey(PreferenceKeys.PLAYER_VOLUME.Name);
    }
}
