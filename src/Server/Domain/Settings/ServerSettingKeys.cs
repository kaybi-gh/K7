using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Settings;

public static class ServerSettingKeys
{
    public static readonly SettingKey<bool> SetupCompleted = new("SetupCompleted");
    public static readonly SettingKey<string> SetupTokenHash = new("SetupTokenHash");
    /// <summary>
    /// Plaintext setup token kept only until first-run setup completes, so it can be re-logged on every restart.
    /// Removed together with <see cref="SetupTokenHash"/> when setup finishes.
    /// </summary>
    public static readonly SettingKey<string> SetupToken = new("SetupToken");
    public static readonly SettingKey<string> DefaultLanguage = new("DefaultLanguage", "en");
    public static readonly SettingKey<string> DefaultTheme = new("DefaultTheme", "default-dark");
    public static readonly SettingKey<int> BackgroundTaskWorkerCount = new("BackgroundTaskWorkerCount", BackgroundTaskScheduling.DefaultWorkerCount);

    /// <summary>
    /// Operator-configured parallelism per lane. Stored under a new key: the previous
    /// "BackgroundTaskConcurrencyLimits" was a free-form dictionary keyed by group name and cannot be
    /// mapped one-to-one onto the typed lanes.
    /// </summary>
    public static readonly SettingKey<Dictionary<BackgroundTaskLane, int>> BackgroundTaskLaneLimits = new("BackgroundTaskLaneLimits", new());
    public static readonly SettingKey<string> HomeLayout = new("HomeLayout");
    public static readonly SettingKey<string> FeatureFlags = new("FeatureFlags");
    public static readonly SettingKey<string> VideoPlayerSettings = new("VideoPlayerSettings");
    public static readonly SettingKey<string> AudioPlayerSettings = new("AudioPlayerSettings");
    public static readonly SettingKey<string> VideoPlaybackPolicy = new("VideoPlaybackPolicy");
    public static readonly SettingKey<string> AudioPlaybackPolicy = new("AudioPlaybackPolicy");
    public static readonly SettingKey<string> TrackSelectionPreferences = new("TrackSelectionPreferences");
    public static readonly SettingKey<string> AudioMuseAi = new("AudioMuseAi");
    public static readonly SettingKey<string> TranscodeSettings = new("TranscodeSettings");
    public static readonly SettingKey<string> FederationSocialPolicy = new("FederationSocialPolicy");
}
