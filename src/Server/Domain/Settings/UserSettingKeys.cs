namespace K7.Server.Domain.Settings;

public static class UserSettingKeys
{
    public static readonly SettingKey<string> Language = new("Language");
    public static readonly SettingKey<string> HomeLayout = new("HomeLayout");
    public static readonly SettingKey<string> VideoPlayerSettings = new("VideoPlayerSettings");
    public static readonly SettingKey<string> TrackSelectionPreferences = new("TrackSelectionPreferences");
    public static readonly SettingKey<string> SyncPlayPreferences = new("SyncPlayPreferences");
    public static readonly SettingKey<string> SharedProfilePreferences = new("SharedProfilePreferences");
}
