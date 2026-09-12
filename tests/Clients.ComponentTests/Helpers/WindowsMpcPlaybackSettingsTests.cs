using AwesomeAssertions;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class WindowsMpcPlaybackSettingsTests
{
    [Test]
    public void Load_ShouldReturnDefaults_WhenStoreEmpty()
    {
        var storage = new MemoryDeviceStorage();

        var options = WindowsMpcPlaybackSettings.Load(storage);

        options.Enabled.Should().BeFalse();
        options.ExePath.Should().BeEmpty();
        options.WebHost.Should().Be(WindowsMpcPlaybackSettings.DefaultWebHost);
        options.WebPort.Should().Be(WindowsMpcPlaybackSettings.DefaultWebPort);
        options.ExtraArgs.Should().Be(WindowsMpcPlaybackSettings.DefaultExtraArgs);
        WindowsMpcPlaybackSettings.IsDefault(options).Should().BeTrue();
    }

    [Test]
    public void Save_ShouldRoundTrip()
    {
        var storage = new MemoryDeviceStorage();
        var written = new WindowsMpcPlaybackOptions(
            true,
            @"C:\Program Files\MPC-HC\mpc-hc64.exe",
            "127.0.0.1",
            13580,
            "/fullscreen");

        WindowsMpcPlaybackSettings.Save(storage, written);

        WindowsMpcPlaybackSettings.IsEnabled(storage).Should().BeTrue();
        WindowsMpcPlaybackSettings.Load(storage).Should().Be(written);
    }

    [Test]
    public void Reset_ShouldRestoreDefaults()
    {
        var storage = new MemoryDeviceStorage();
        WindowsMpcPlaybackSettings.Save(storage, new WindowsMpcPlaybackOptions(
            true, @"D:\mpc.exe", "192.168.1.2", 80, "/play"));

        WindowsMpcPlaybackSettings.Reset(storage);

        WindowsMpcPlaybackSettings.IsDefault(WindowsMpcPlaybackSettings.Load(storage)).Should().BeTrue();
    }

    private sealed class MemoryDeviceStorage : IDeviceStorageService
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public T? Get<T>(PreferenceKey<T> key, T? defaultValue = default)
        {
            if (_values.TryGetValue(key.Name, out var value) && value is T typed)
                return typed;

            return defaultValue;
        }

        public void Set<T>(PreferenceKey<T> key, T value) => _values[key.Name] = value;

        public void Remove<T>(PreferenceKey<T> key) => _values.Remove(key.Name);

        public void ClearAllPreferences() => _values.Clear();
    }
}
