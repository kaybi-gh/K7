namespace K7.Shared;

public sealed class PreferenceKey<T>(string name)
{
    public string Name { get; } = name;
}

public static class PreferenceKeys
{
    public static readonly PreferenceKey<string> K7_SERVER_URL = new("BackendUrl");
    public static readonly PreferenceKey<string> DEVICE_ID = new("DeviceId");
    public static readonly PreferenceKey<double> PLAYER_VOLUME = new("PlayerVolume");
    public static readonly PreferenceKey<double> PLAYER_PLAYBACK_RATE = new("PlayerPlaybackRate");
    public static readonly PreferenceKey<bool> PLAYER_IS_MUTED = new("PlayerIsMuted");
}
