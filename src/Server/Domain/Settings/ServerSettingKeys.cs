namespace K7.Server.Domain.Settings;

public static class ServerSettingKeys
{
    public static readonly SettingKey<bool> SetupCompleted = new("SetupCompleted");
    public static readonly SettingKey<string> SetupTokenHash = new("SetupTokenHash");
    public static readonly SettingKey<string> DefaultLanguage = new("DefaultLanguage", "en");
    public static readonly SettingKey<string> DefaultTheme = new("DefaultTheme", "default-dark");
    public static readonly SettingKey<int> BackgroundTaskWorkerCount = new("BackgroundTaskWorkerCount", 3);
    public static readonly SettingKey<Dictionary<string, int>> BackgroundTaskConcurrencyLimits = new("BackgroundTaskConcurrencyLimits", new());
    public static readonly SettingKey<string> HomeLayout = new("HomeLayout");
    public static readonly SettingKey<string> FeatureFlags = new("FeatureFlags");
    public static readonly SettingKey<string> VideoPlayerSettings = new("VideoPlayerSettings");
    public static readonly SettingKey<string> AudioPlayerSettings = new("AudioPlayerSettings");
    public static readonly SettingKey<string> VideoPlaybackPolicy = new("VideoPlaybackPolicy");
    public static readonly SettingKey<string> AudioPlaybackPolicy = new("AudioPlaybackPolicy");
    public static readonly SettingKey<string> TrackSelectionPreferences = new("TrackSelectionPreferences");
    public static readonly SettingKey<string> AudioMuseAi = new("AudioMuseAi");
    public static readonly SettingKey<string> FederationSocialPolicy = new("FederationSocialPolicy");
}
