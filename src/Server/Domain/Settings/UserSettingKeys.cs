namespace K7.Server.Domain.Settings;

public static class UserSettingKeys
{
    public static readonly SettingKey<string> Language = new("Language");
    public static readonly SettingKey<string> HomeLayout = new("HomeLayout");
    public static readonly SettingKey<string> VideoPlayerSettings = new("VideoPlayerSettings");
    public static readonly SettingKey<string> AudioPlayerSettings = new("AudioPlayerSettings");
    public static readonly SettingKey<string> VideoPlaybackPolicy = new("VideoPlaybackPolicy");
    public static readonly SettingKey<string> AudioPlaybackPolicy = new("AudioPlaybackPolicy");
    public static readonly SettingKey<string> TrackSelectionPreferences = new("TrackSelectionPreferences");
    public static readonly SettingKey<string> SyncPlayPreferences = new("SyncPlayPreferences");
    public static readonly SettingKey<string> SharedProfilePreferences = new("SharedProfilePreferences");
    public static readonly SettingKey<string> FederationPrivacy = new("FederationPrivacy");
    public static readonly SettingKey<string> ReviewPreferences = new("ReviewPreferences");
}
