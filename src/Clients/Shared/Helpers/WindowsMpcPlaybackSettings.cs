using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Helpers;

namespace K7.Clients.Shared.Helpers;

public sealed record WindowsMpcPlaybackOptions(
    bool Enabled,
    string ExePath,
    string WebHost,
    int WebPort,
    string ExtraArgs,
    string LibraryLocalRootsJson);

public static class WindowsMpcPlaybackSettings
{
    public const string DefaultWebHost = "127.0.0.1";
    public const int DefaultWebPort = 13579;
    public const string DefaultExtraArgs = "/fullscreen /close";

    public static bool IsEnabled(IDeviceStorageService storage) =>
        storage.Get(PreferenceKeys.VIDEO_MPC_ENABLED, false);

    public static WindowsMpcPlaybackOptions Load(IDeviceStorageService storage)
    {
        var host = storage.Get(PreferenceKeys.VIDEO_MPC_WEB_HOST, DefaultWebHost);
        if (string.IsNullOrWhiteSpace(host))
            host = DefaultWebHost;

        var port = storage.Get(PreferenceKeys.VIDEO_MPC_WEB_PORT, DefaultWebPort);
        if (port is < 1 or > 65535)
            port = DefaultWebPort;

        var extra = storage.Get(PreferenceKeys.VIDEO_MPC_EXTRA_ARGS, DefaultExtraArgs);
        extra ??= DefaultExtraArgs;

        return new WindowsMpcPlaybackOptions(
            Enabled: storage.Get(PreferenceKeys.VIDEO_MPC_ENABLED, false),
            ExePath: storage.Get(PreferenceKeys.VIDEO_MPC_EXE_PATH, "") ?? "",
            WebHost: host.Trim(),
            WebPort: port,
            ExtraArgs: extra,
            LibraryLocalRootsJson: storage.Get(PreferenceKeys.VIDEO_MPC_LIBRARY_PATHS, "") ?? "");
    }

    public static void Save(IDeviceStorageService storage, WindowsMpcPlaybackOptions options)
    {
        storage.Set(PreferenceKeys.VIDEO_MPC_ENABLED, options.Enabled);
        storage.Set(PreferenceKeys.VIDEO_MPC_EXE_PATH, options.ExePath.Trim());
        storage.Set(PreferenceKeys.VIDEO_MPC_WEB_HOST, string.IsNullOrWhiteSpace(options.WebHost)
            ? DefaultWebHost
            : options.WebHost.Trim());
        storage.Set(PreferenceKeys.VIDEO_MPC_WEB_PORT, options.WebPort is >= 1 and <= 65535
            ? options.WebPort
            : DefaultWebPort);
        storage.Set(PreferenceKeys.VIDEO_MPC_EXTRA_ARGS, options.ExtraArgs);
        storage.Set(PreferenceKeys.VIDEO_MPC_LIBRARY_PATHS, options.LibraryLocalRootsJson ?? "");
    }

    public static void Reset(IDeviceStorageService storage)
    {
        Save(storage, Empty());
    }

    public static WindowsMpcPlaybackOptions Empty() =>
        new(
            Enabled: false,
            ExePath: "",
            WebHost: DefaultWebHost,
            WebPort: DefaultWebPort,
            ExtraArgs: DefaultExtraArgs,
            LibraryLocalRootsJson: "");

    public static bool IsDefault(WindowsMpcPlaybackOptions options) =>
        !options.Enabled
        && string.IsNullOrWhiteSpace(options.ExePath)
        && string.Equals(options.WebHost, DefaultWebHost, StringComparison.OrdinalIgnoreCase)
        && options.WebPort == DefaultWebPort
        && options.ExtraArgs == DefaultExtraArgs
        && LibraryPathMirror.Deserialize(options.LibraryLocalRootsJson).Count == 0;

    public static IReadOnlyDictionary<Guid, string> LibraryLocalRoots(WindowsMpcPlaybackOptions options) =>
        LibraryPathMirror.Deserialize(options.LibraryLocalRootsJson);
}
