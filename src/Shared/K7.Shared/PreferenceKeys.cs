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
    public static readonly PreferenceKey<bool> PLAYER_ADAPTIVE_CROSSFADE = new("PlayerAdaptiveCrossfade");
    public static readonly PreferenceKey<double> PLAYER_CROSSFADE_DURATION = new("PlayerCrossfadeDuration");
    public static readonly PreferenceKey<string> ACCESS_TOKEN = new("AccessToken");
    public static readonly PreferenceKey<string> REFRESH_TOKEN = new("RefreshToken");
    public static readonly PreferenceKey<string> SERVER_INFO = new("ServerInfo");
    public static readonly PreferenceKey<string> LOCAL_USERS = new("LocalUsers");
    public static readonly PreferenceKey<bool> SINGLE_USER_MODE = new("SingleUserMode");
    public static readonly PreferenceKey<string> LAST_ACTIVE_USER_ID = new("LastActiveUserId");
    public static readonly PreferenceKey<string> NEXT_EPISODE_BEHAVIOR = new("NextEpisodeBehavior");
}
